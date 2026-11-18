using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

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

    /// <summary>Retrieves a previously processed payment.</summary>
    /// <param name="id">The payment ID.</param>
    /// <returns>The masked payment details.</returns>
    /// <response code="200">Payment found.</response>
    /// <response code="404">No payment exists for the supplied ID.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>Processes a card payment through the acquiring bank.</summary>
    /// <param name="request">The payment and card details.</param>
    /// <param name="idempotencyKey">An optional key for exactly-once request reuse; 1-64 characters.</param>
    /// <param name="cancellationToken">Cancels the bank request when the caller aborts.</param>
    /// <response code="200">Payment authorized or declined.</response>
    /// <response code="400">The request or idempotency header is invalid.</response>
    /// <response code="409">The idempotency key is in progress or used with different payment data.</response>
    /// <response code="502">The acquiring bank is unavailable or returned an invalid response.</response>
    /// <response code="504">The acquiring bank request timed out.</response>
    [HttpPost]
    [ProducesResponseType<PostPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        [FromBody] PostPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key"), Description("Optional 1-64 character key for request reuse.")] string? idempotencyKey,
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