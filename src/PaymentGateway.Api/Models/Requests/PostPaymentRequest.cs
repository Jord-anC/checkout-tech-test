using System.ComponentModel.DataAnnotations;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Models.Requests;

/// <summary>Card payment details supplied by a merchant.</summary>
public class PostPaymentRequest : IValidatableObject
{
    /// <summary>Full card number. It must contain 14-19 numeric digits.</summary>
    [Required(ErrorMessage = "Card number is required.")]
    [RegularExpression(@"^[0-9]{14,19}$", ErrorMessage = "Card number must contain 14 to 19 numeric digits.")]
    public string? CardNumber { get; set; }

    /// <summary>Card expiry month from 1 to 12.</summary>
    [Required(ErrorMessage = "Expiry month is required.")]
    [Range(1, 12, ErrorMessage = "Expiry month must be between 1 and 12.")]
    public int? ExpiryMonth { get; set; }

    /// <summary>Card expiry year. The combined month and year must be in the future.</summary>
    [Required(ErrorMessage = "Expiry year is required.")]
    [Range(1, 9999, ErrorMessage = "Expiry year must be a valid year.")]
    public int? ExpiryYear { get; set; }

    /// <summary>Supported ISO-style currency code: GBP, USD, or EUR.</summary>
    [Required(ErrorMessage = "Currency is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a three-character code.")]
    public string? Currency { get; set; }

    /// <summary>Positive amount in minor currency units, for example 1050 for $10.50.</summary>
    [Required(ErrorMessage = "Amount is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Amount must be a positive whole number in minor currency units.")]
    public int? Amount { get; set; }

    /// <summary>Three- or four-digit card verification value.</summary>
    [Required(ErrorMessage = "CVV is required.")]
    [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV must contain 3 or 4 numeric digits.")]
    public string? Cvv { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Currency is { Length: 3 } && !SupportedCurrencies.All.Contains(Currency))
        {
            yield return new ValidationResult(
                "The currency is not supported.",
                [nameof(Currency)]);
        }

        if (ExpiryMonth.HasValue && ExpiryYear.HasValue &&
            (ExpiryYear.Value < DateTime.UtcNow.Year ||
             (ExpiryYear.Value == DateTime.UtcNow.Year && ExpiryMonth.Value < DateTime.UtcNow.Month)))
        {
            yield return new ValidationResult(
                "The card expiry date must be in the future.",
                [nameof(ExpiryMonth), nameof(ExpiryYear)]);
        }
    }
}