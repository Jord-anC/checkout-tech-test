using System.Collections.Concurrent;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public sealed class IdempotencyReservation
{
    public IdempotencyReservationStatus Status { get; init; }
    public PostPaymentResponse? Payment { get; init; }
}

public enum IdempotencyReservationStatus
{
    Started,
    Reused,
    Conflict,
    InProgress
}

public sealed class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PostPaymentResponse> _payments = new();
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _idempotencyKeys = new();

    public void Add(PostPaymentResponse payment) => _payments[payment.Id] = payment;

    public PostPaymentResponse? Get(Guid id) => _payments.GetValueOrDefault(id);

    public IdempotencyReservation TryReserveIdempotencyKey(string key, string requestFingerprint)
    {
        var newEntry = new IdempotencyEntry(requestFingerprint, null);

        if (_idempotencyKeys.TryAdd(key, newEntry))
        {
            return new IdempotencyReservation { Status = IdempotencyReservationStatus.Started };
        }

        if (!_idempotencyKeys.TryGetValue(key, out var existing))
        {
            return new IdempotencyReservation { Status = IdempotencyReservationStatus.Conflict };
        }

        if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return new IdempotencyReservation { Status = IdempotencyReservationStatus.Conflict };
        }

        return existing.Payment is null
            ? new IdempotencyReservation { Status = IdempotencyReservationStatus.InProgress }
            : new IdempotencyReservation
            {
                Status = IdempotencyReservationStatus.Reused,
                Payment = existing.Payment
            };
    }

    public void CompleteIdempotencyKey(string key, PostPaymentResponse payment)
    {
        _idempotencyKeys.AddOrUpdate(
            key,
            _ => new IdempotencyEntry(string.Empty, payment),
            (_, existing) => new IdempotencyEntry(existing.RequestFingerprint, payment));
    }

    public void ReleaseIdempotencyKey(string key, string requestFingerprint)
    {
        if (_idempotencyKeys.TryGetValue(key, out var existing) &&
            existing.Payment is null &&
            string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            _idempotencyKeys.TryRemove(key, out _);
        }
    }

    private sealed record IdempotencyEntry(string RequestFingerprint, PostPaymentResponse? Payment);
}