using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Models.Bank;

public sealed class BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool? Authorized { get; init; }
}