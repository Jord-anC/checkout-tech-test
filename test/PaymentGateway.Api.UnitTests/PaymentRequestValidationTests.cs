using System.ComponentModel.DataAnnotations;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.UnitTests;

public class PaymentRequestValidationTests
{
    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    public void AcceptsCardNumberWithFourteenToNineteenDigits(int length)
    {
        var request = CreateValidRequest();
        request.CardNumber = new string('1', length);

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void RejectsCardNumberShorterThanFourteenDigits()
    {
        var request = CreateValidRequest();
        request.CardNumber = new string('1', 13);

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.CardNumber)));
    }

    [Fact]
    public void RejectsCardNumberLongerThanNineteenDigits()
    {
        var request = CreateValidRequest();
        request.CardNumber = new string('1', 20);

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.CardNumber)));
    }

    [Fact]
    public void RejectsNonNumericCardNumber()
    {
        var request = CreateValidRequest();
        request.CardNumber = "22224053432488A7";

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.CardNumber)));
    }

    [Fact]
    public void ReturnsHelpfulCardNumberValidationMessage()
    {
        var request = CreateValidRequest();
        request.CardNumber = "invalid";

        Assert.Contains(Validate(request), result =>
            result.MemberNames.Contains(nameof(request.CardNumber)) &&
            result.ErrorMessage == "Card number must contain 14 to 19 numeric digits.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void RejectsExpiryMonthOutsideOneThroughTwelve(int month)
    {
        var request = CreateValidRequest();
        request.ExpiryMonth = month;

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.ExpiryMonth)));
    }

    [Fact]
    public void ReturnsHelpfulExpiryMonthValidationMessage()
    {
        var request = CreateValidRequest();
        request.ExpiryMonth = 13;

        Assert.Contains(Validate(request), result =>
            result.MemberNames.Contains(nameof(request.ExpiryMonth)) &&
            result.ErrorMessage == "Expiry month must be between 1 and 12.");
    }

    [Fact]
    public void RejectsExpiryYearInThePast()
    {
        var request = CreateValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.ExpiryYear)));
    }

    [Fact]
    public void RejectsExpiryMonthThatHasPassedInTheCurrentYear()
    {
        var pastDate = DateTime.UtcNow.AddMonths(-1);
        var request = CreateValidRequest();
        request.ExpiryYear = pastDate.Year;
        request.ExpiryMonth = pastDate.Month;

        Assert.Contains(Validate(request), result =>
            result.MemberNames.Contains(nameof(request.ExpiryMonth)) &&
            result.MemberNames.Contains(nameof(request.ExpiryYear)));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void AcceptsEachOfThreeSupportedCurrencies(string currency)
    {
        var request = CreateValidRequest();
        request.Currency = currency;

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void SupportsNoMoreThanThreeCurrencies()
    {
        Assert.InRange(SupportedCurrencies.All.Count, 1, 3);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("CAD")]
    [InlineData("gbp")]
    public void RejectsCurrenciesOutsideThreeSupportedCurrencyCodes(string currency)
    {
        var request = CreateValidRequest();
        request.Currency = currency;

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.Currency)));
    }

    [Fact]
    public void ReturnsHelpfulCurrencyLengthValidationMessage()
    {
        var request = CreateValidRequest();
        request.Currency = "US";

        Assert.Contains(Validate(request), result =>
            result.MemberNames.Contains(nameof(request.Currency)) &&
            result.ErrorMessage == "Currency must be a three-character code.");
    }

    [Fact]
    public void ReturnsHelpfulRequiredFieldValidationMessage()
    {
        var request = CreateValidRequest();
        request.Cvv = null;

        Assert.Contains(Validate(request), result =>
            result.MemberNames.Contains(nameof(request.Cvv)) &&
            result.ErrorMessage == "CVV is required.");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0123")]
    public void AcceptsThreeOrFourDigitCVV(string cvv)
    {
        var request = CreateValidRequest();
        request.Cvv = cvv;

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12A")]
    public void RejectsCVVThatIsNotThreeOrFourNumericDigits(string cvv)
    {
        var request = CreateValidRequest();
        request.Cvv = cvv;

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.Cvv)));
    }

    [Fact]
    public void RejectsZeroAmount()
    {
        var request = CreateValidRequest();
        request.Amount = 0;

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.Amount)));
    }

    private static IReadOnlyCollection<ValidationResult> Validate(PostPaymentRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static PostPaymentRequest CreateValidRequest()
    {
        var futureDate = DateTime.UtcNow.AddMonths(1);

        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = futureDate.Month,
            ExpiryYear = futureDate.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "012"
        };
    }
}