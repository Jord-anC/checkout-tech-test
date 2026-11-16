namespace PaymentGateway.Api.UnitTests;

public class PaymentRequestValidationTests
{
    [Fact(Skip = "Pending implementation")]
    public void AcceptsCardNumberWithFourteenToNineteenDigits()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsCardNumberShorterThanFourteenDigits()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsCardNumberLongerThanNineteenDigits()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsNonNumericCardNumber()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsExpiryMonthOutsideOneThroughTwelve()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsExpiryYearInThePast()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsExpiryMonthThatHasPassedInTheCurrentYear()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsCurrencyThatIsNotThreeCharacters()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsCurrencyOutsideSupportedCurrencyList()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void SupportsNoMoreThanThreeCurrencies()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void AcceptsThreeDigitCVV()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void AcceptsFourDigitCVV()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsCVVShorterThanThreeDigits()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsCVVLongerThanFourDigits()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsNonNumericCVV()
    {
    }
}