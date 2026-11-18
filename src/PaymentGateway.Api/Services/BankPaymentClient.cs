using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class BankPaymentClient(HttpClient httpClient) : IBankPaymentClient
{
    private const int MaxRetryAttempts = 2;

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

        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                response = await httpClient.PostAsJsonAsync("payments", bankRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                if (!IsTransient(response.StatusCode) || attempt == MaxRetryAttempts)
                {
                    response.Dispose();
                    throw new HttpRequestException(
                        $"The bank returned HTTP {(int)response.StatusCode}.",
                        null,
                        response.StatusCode);
                }

                response.Dispose();
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is null && attempt < MaxRetryAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested && attempt < MaxRetryAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        if (response is null)
        {
            throw new HttpRequestException("The bank request failed after the retry limit.");
        }

        using (response)
        {
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

            return bankResponse?.Authorized
                ?? throw new HttpRequestException("The bank returned an invalid response.");
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var baseDelayMilliseconds = 100 * (1 << attempt);
        var jitterMilliseconds = Random.Shared.Next(0, 50);
        await Task.Delay(baseDelayMilliseconds + jitterMilliseconds, cancellationToken);
    }
}