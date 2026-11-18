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
        CancellationToken cancellationToken)
    {
        bool authorized;

        try
        {
            authorized = await _bankPaymentClient.AuthorizeAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "Payment processor timed out.");
        }
        catch (HttpRequestException)
        {
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

        return Ok(payment);
    }
}