namespace PaymentGateway.Api.IntegrationTests;

public class PaymentProcessingIntegrationTests
{
    [Fact(Skip = "Pending implementation")]
    public void ReturnsAuthorizedPaymentDetailsWhenBankAuthorizesPayment()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void ReturnsDeclinedPaymentDetailsWhenBankDeclinesPayment()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsInvalidPaymentRequest()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsRequestWhenCardNumberIsMissing()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsRequestWhenExpiryMonthIsMissing()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsRequestWhenExpiryYearIsMissing()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsRequestWhenCurrencyIsMissing()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsRequestWhenAmountIsMissing()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsRequestWhenCVVIsMissing()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void RejectsNonIntegerAmount()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void ReturnsGatewayFailureWhenBankIsUnavailable()
    {
    }

    [Fact(Skip = "Pending implementation")]
    public void ReturnsMaskedCardDetailsWhenRetrievingPayment()
    {
    }
}