using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    void Add(PostPaymentResponse payment);

    PostPaymentResponse? Get(Guid id);

    IdempotencyReservation TryReserveIdempotencyKey(string key, string requestFingerprint);

    void CompleteIdempotencyKey(string key, PostPaymentResponse payment);

    void ReleaseIdempotencyKey(string key, string requestFingerprint);
}