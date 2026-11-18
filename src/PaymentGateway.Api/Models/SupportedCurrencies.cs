using System.Collections.Frozen;

namespace PaymentGateway.Api.Models;

public static class SupportedCurrencies
{
    public static FrozenSet<string> All { get; } = new[] { "GBP", "USD", "EUR" }
        .ToFrozenSet(StringComparer.Ordinal);
}