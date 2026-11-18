using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class BankPaymentClient(HttpClient httpClient) : IBankPaymentClient
{
    public async Task<bool> AuthorizeAsync(PostPaymentRequest payment, CancellationToken cancellationToken)
    {
        var bankRequest = new BankPaymentRequest
        {
            CardNumber = payment.CardNumber!,
            ExpiryDate = new DateTime(payment.ExpiryYear!.Value, payment.ExpiryMonth!.Value, 1)
                .ToString("MM/yyyy", CultureInfo.InvariantCulture),
            Currency = payment.Currency!,
            Amount = payment.Amount!.Value,
            Cvv = payment.Cvv!
        };

        using var response = await httpClient.PostAsJsonAsync("payments", bankRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        BankPaymentResponse? bankResponse;
        try
        {
            bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException("The bank returned an invalid response.", exception);
        }

        return bankResponse?.Authorized ?? throw new HttpRequestException("The bank returned an invalid response.");
    }
}