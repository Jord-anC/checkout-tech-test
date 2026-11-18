namespace PaymentGateway.Api.Models.Responses;

/// <summary>Masked result of a processed payment.</summary>
public class PostPaymentResponse
{
    /// <summary>Payment identifier used to retrieve the payment.</summary>
    public Guid Id { get; set; }

    /// <summary>Payment status: Authorized or Declined.</summary>
    public PaymentStatus Status { get; set; }

    /// <summary>Last four digits of the card number.</summary>
    public string CardNumberLastFour { get; set; } = string.Empty;

    /// <summary>Card expiry month.</summary>
    public int ExpiryMonth { get; set; }

    /// <summary>Card expiry year.</summary>
    public int ExpiryYear { get; set; }

    /// <summary>Currency code used for the payment.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Amount in minor currency units.</summary>
    public int Amount { get; set; }
}
