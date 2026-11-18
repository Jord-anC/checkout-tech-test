using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();

var bankBaseUrl = builder.Configuration["BankSimulator:BaseUrl"];
var bankTimeoutSeconds = builder.Configuration.GetValue("BankSimulator:TimeoutSeconds", 5);

builder.Services.AddHttpClient<IBankPaymentClient, BankPaymentClient>(client =>
{
    if (string.IsNullOrWhiteSpace(bankBaseUrl) ||
        !Uri.TryCreate(bankBaseUrl, UriKind.Absolute, out var absoluteBankBaseUrl))
    {
        throw new InvalidOperationException("BankSimulator:BaseUrl must be configured as an absolute URL.");
    }

    if (bankTimeoutSeconds <= 0)
    {
        throw new InvalidOperationException("BankSimulator:TimeoutSeconds must be greater than zero.");
    }

    client.BaseAddress = absoluteBankBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(bankTimeoutSeconds);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
