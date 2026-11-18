using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.UnitTests;

public class PaymentProcessorTests
{
    [Fact]
    public async Task MapsAuthorizedBankResponseToAuthorizedPayment()
    {
        var repository = new TestPaymentsRepository();
        var controller = new PaymentsController(repository, new StubBankPaymentClient(authorized: true));

        var result = await controller.PostPaymentAsync(CreateValidRequest(), null, CancellationToken.None);

        var payment = GetPayment(result);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(payment.Id, repository.Get(payment.Id)?.Id);
    }

    [Fact]
    public async Task MapsDeclinedBankResponseToDeclinedPayment()
    {
        var repository = new TestPaymentsRepository();
        var controller = new PaymentsController(repository, new StubBankPaymentClient(authorized: false));

        var result = await controller.PostPaymentAsync(CreateValidRequest(), null, CancellationToken.None);

        var payment = GetPayment(result);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(payment.Id, repository.Get(payment.Id)?.Id);
    }

    [Fact]
    public async Task StoresPaymentDetailsWithoutFullCardNumberOrCVV()
    {
        var repository = new TestPaymentsRepository();
        var controller = new PaymentsController(repository, new StubBankPaymentClient(authorized: true));
        var request = CreateValidRequest();

        var result = await controller.PostPaymentAsync(request, null, CancellationToken.None);

        var payment = GetPayment(result);
        var stored = Assert.IsType<PostPaymentResponse>(repository.Get(payment.Id));

        Assert.Equal("8877", stored.CardNumberLastFour);

        var serializedPayment = JsonSerializer.Serialize(stored);
        Assert.DoesNotContain(request.CardNumber!, serializedPayment);
        Assert.DoesNotContain(request.Cvv!, serializedPayment);
    }

    private static PostPaymentResponse GetPayment(ActionResult<PostPaymentResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PostPaymentResponse>(ok.Value);
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

    private sealed class StubBankPaymentClient(bool authorized) : IBankPaymentClient
    {
        public Task<bool> AuthorizeAsync(PostPaymentRequest payment, CancellationToken cancellationToken)
            => Task.FromResult(authorized);
    }

    private sealed class TestPaymentsRepository : IPaymentsRepository
    {
        private readonly Dictionary<Guid, PostPaymentResponse> _payments = [];

        public void Add(PostPaymentResponse payment) => _payments[payment.Id] = payment;

        public PostPaymentResponse? Get(Guid id) => _payments.GetValueOrDefault(id);

        public IdempotencyReservation TryReserveIdempotencyKey(string key, string requestFingerprint)
            => new() { Status = IdempotencyReservationStatus.Started };

        public void CompleteIdempotencyKey(string key, PostPaymentResponse payment)
        {
        }

        public void ReleaseIdempotencyKey(string key, string requestFingerprint)
        {
        }
    }
}