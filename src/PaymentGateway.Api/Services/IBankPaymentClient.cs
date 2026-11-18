using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IBankPaymentClient
{
    Task<bool> AuthorizeAsync(PostPaymentRequest payment, CancellationToken cancellationToken);
}