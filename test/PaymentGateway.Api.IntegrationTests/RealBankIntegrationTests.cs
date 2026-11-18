using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.IntegrationTests;

public class RealBankIntegrationTests
{
    [Fact]
    public async Task SuccessfulPaymentPathUsingRealBankClient()
    {
        using var factory = new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var request = new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = 2099,
            Currency = "GBP",
            Amount = 1050,
            Cvv = "123"
        };

        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PostPaymentResponse>()
            ?? throw new InvalidOperationException("The payment response was empty.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);

        var getResponse = await client.GetAsync($"/api/Payments/{payment.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
