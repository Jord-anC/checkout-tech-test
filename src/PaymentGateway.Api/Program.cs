using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<PaymentStatus>(() => new OpenApiSchema
    {
        Type = "string",
        Enum =
        [
            new Microsoft.OpenApi.Any.OpenApiString("Authorized"),
            new Microsoft.OpenApi.Any.OpenApiString("Declined")
        ]
    });

    options.MapType<ProblemDetails>(() => new OpenApiSchema
    {
        Type = "object",
        AdditionalPropertiesAllowed = true,
        Description = "Validation or processing problem details."
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

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
