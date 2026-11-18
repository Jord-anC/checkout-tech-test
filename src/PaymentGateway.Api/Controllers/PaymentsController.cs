using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IBankPaymentClient _bankPaymentClient;

    public PaymentsController(IPaymentsRepository paymentsRepository, IBankPaymentClient bankPaymentClient)
    {
        _paymentsRepository = paymentsRepository;
        _bankPaymentClient = bankPaymentClient;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GetPaymentResponse> GetPaymentAsync(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment is null)
        {
            return NotFound();
        }

        var response = new GetPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var reservation = default(IdempotencyReservation);

        if (idempotencyKey is not null)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
            {
                return BadRequest("Idempotency-Key must contain 1 to 64 characters.");
            }

            var fingerprint = ComputeRequestFingerprint(request);
            reservation = _paymentsRepository.TryReserveIdempotencyKey(idempotencyKey, fingerprint);

            if (reservation.Status == IdempotencyReservationStatus.Reused)
            {
                return Ok(reservation.Payment);
            }

            if (reservation.Status == IdempotencyReservationStatus.Conflict)
            {
                return Conflict("The Idempotency-Key has already been used with different payment data.");
            }

            if (reservation.Status == IdempotencyReservationStatus.InProgress)
            {
                return Conflict("The payment for this Idempotency-Key is already being processed.");
            }
        }

        bool authorized;

        try
        {
            authorized = await _bankPaymentClient.AuthorizeAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ReleaseIdempotencyKey(idempotencyKey, request);
            return Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "Payment processor timed out.");
        }
        catch (HttpRequestException)
        {
            ReleaseIdempotencyKey(idempotencyKey, request);
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Payment processor unavailable.");
        }

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber![^4..],
            ExpiryMonth = request.ExpiryMonth!.Value,
            ExpiryYear = request.ExpiryYear!.Value,
            Currency = request.Currency!,
            Amount = request.Amount!.Value
        };

        _paymentsRepository.Add(payment);

        if (idempotencyKey is not null)
        {
            _paymentsRepository.CompleteIdempotencyKey(idempotencyKey, payment);
        }

        return Ok(payment);
    }

    private void ReleaseIdempotencyKey(string? idempotencyKey, PostPaymentRequest request)
    {
        if (idempotencyKey is not null)
        {
            _paymentsRepository.ReleaseIdempotencyKey(idempotencyKey, ComputeRequestFingerprint(request));
        }
    }

    private static string ComputeRequestFingerprint(PostPaymentRequest request)
    {
        var canonical = string.Join("|", request.CardNumber, request.ExpiryMonth, request.ExpiryYear,
            request.Currency, request.Amount, request.Cvv);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }
}