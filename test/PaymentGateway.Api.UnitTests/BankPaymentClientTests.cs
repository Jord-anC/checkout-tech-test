using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.UnitTests;

public class BankPaymentClientTests
{
    [Fact]
    public async Task SendsSimulatorContractAndReturnsAuthorizationDecision()
    {
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("http://bank.test/payments"), request.RequestUri);

            using var body = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var json = body.RootElement;

            Assert.Equal("2222405343248877", json.GetProperty("card_number").GetString());
            Assert.Equal("04/2099", json.GetProperty("expiry_date").GetString());
            Assert.Equal("GBP", json.GetProperty("currency").GetString());
            Assert.Equal(1050, json.GetProperty("amount").GetInt32());
            Assert.Equal("012", json.GetProperty("cvv").GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { authorized = true, authorization_code = "bank-reference" })
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        var authorized = await bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None);

        Assert.True(authorized);
    }

    [Fact]
    public async Task SendsSimulatorContractAndReturnsDeclinedDecision()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { authorized = false, authorization_code = "" })
        }));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        var authorized = await bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None);

        Assert.False(authorized);
    }

    [Fact]
    public async Task RetriesTransientFailureThenReturnsAuthorizationDecision()
    {
        var attempt = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            attempt++;
            var response = attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { authorized = true, authorization_code = "bank-reference" })
                };

            return Task.FromResult(response);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        var authorized = await bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None);

        Assert.True(authorized);
        Assert.Equal(2, attempt);
    }

    [Fact]
    public async Task DoesNotRetryPermanentBadRequest()
    {
        var attempt = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            attempt++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None));

        Assert.Equal(1, attempt);
    }

    [Fact]
    public async Task RetriesTransientNetworkFailureThenSucceeds()
    {
        var attempt = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            attempt++;

            if (attempt == 1)
            {
                throw new HttpRequestException("The network connection failed.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { authorized = true, authorization_code = "bank-reference" })
            });
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        var authorized = await bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None);

        Assert.True(authorized);
        Assert.Equal(2, attempt);
    }

    [Fact]
    public async Task ThrowsWhenBankReturnsNonSuccessStatus()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowsWhenBankReturnsInvalidJson()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        }));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") };
        var bankClient = new BankPaymentClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            bankClient.AuthorizeAsync(CreateValidRequest(), CancellationToken.None));
    }

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2099,
        Currency = "GBP",
        Amount = 1050,
        Cvv = "012"
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }
}