using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.ComponentTests;

public class PaymentProcessingComponentTests
{
    [Fact]
    public async Task ReturnsAuthorizedPaymentDetailsWhenBankAuthorizesPayment()
    {
        var bankClient = new StubBankPaymentClient(authorized: true);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest());
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>()
            ?? throw new InvalidOperationException("The payment response was empty.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2099, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(1050, payment.Amount);
        Assert.Equal(1, bankClient.Calls);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("2222405343248877", responseBody);
        Assert.DoesNotContain("012", responseBody);
    }

    [Fact]
    public async Task ReturnsDeclinedPaymentDetailsWhenBankDeclinesPayment()
    {
        var bankClient = new StubBankPaymentClient(authorized: false);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest());
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>()
            ?? throw new InvalidOperationException("The payment response was empty.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task AuthorizedAndDeclinedPaymentsCanBeRetrieved(bool bankAuthorizes, PaymentStatus expectedStatus)
    {
        var bankClient = new StubBankPaymentClient(authorized: bankAuthorizes);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var postResponse = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>()
            ?? throw new InvalidOperationException("The payment response was empty.");

        var getResponse = await client.GetAsync($"/api/Payments/{created.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>()
            ?? throw new InvalidOperationException("The retrieved payment was empty.");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(expectedStatus, retrieved.Status);
        Assert.Equal("8877", retrieved.CardNumberLastFour);

        var retrievedBody = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("2222405343248877", retrievedBody);
        Assert.DoesNotContain("012", retrievedBody);
    }

    [Fact]
    public async Task RejectsInvalidPaymentRequestWithoutCallingBank()
    {
        var bankClient = new StubBankPaymentClient(authorized: true);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var request = CreateValidRequest();
        request.CardNumber = "123";

        var response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, bankClient.Calls);
    }

    [Fact]
    public async Task RejectsRequestWhenRequiredFieldsAreMissing()
    {
        var bankClient = new StubBankPaymentClient(authorized: true);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", new { currency = "GBP" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, bankClient.Calls);
    }

    [Fact]
    public async Task RejectsNonIntegerAmountWithoutCallingBank()
    {
        var bankClient = new StubBankPaymentClient(authorized: true);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();
        var request = new
        {
            cardNumber = "2222405343248877",
            expiryMonth = 4,
            expiryYear = 2099,
            currency = "GBP",
            amount = 1050.5,
            cvv = "012"
        };

        var response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, bankClient.Calls);
    }

    [Fact]
    public async Task ReturnsBadGatewayWhenBankIsUnavailable()
    {
        var bankClient = new StubBankPaymentClient(authorized: true, unavailable: true);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsGatewayTimeoutWhenBankCallTimesOut()
    {
        var bankClient = new StubBankPaymentClient(authorized: true, timeout: true);
        using var factory = CreateFactory(bankClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest());

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    private static WebApplicationFactory<PaymentsController> CreateFactory(StubBankPaymentClient bankClient) =>
        new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBankPaymentClient>();
                services.AddSingleton<IBankPaymentClient>(bankClient);
            }));

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2099,
        Currency = "GBP",
        Amount = 1050,
        Cvv = "012"
    };

    private sealed class StubBankPaymentClient(
        bool authorized,
        bool unavailable = false,
        bool timeout = false) : IBankPaymentClient
    {
        public int Calls { get; private set; }

        public Task<bool> AuthorizeAsync(PostPaymentRequest payment, CancellationToken cancellationToken)
        {
            Calls++;

            if (unavailable)
            {
                throw new HttpRequestException("Bank unavailable.");
            }

            if (timeout)
            {
                throw new OperationCanceledException();
            }

            return Task.FromResult(authorized);
        }
    }
}