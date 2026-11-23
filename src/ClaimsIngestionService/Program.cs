using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi.Models;
using Serilog;
using ClaimsIngestionService.Data;
using ClaimsIngestionService.Middleware;
using ClaimsIngestionService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with PHI masking & tenant context
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [Tenant: {TenantId}] [Corr: {CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Healthcare Multi-Tenant Platform - Claims Ingestion Service",
        Version = "v1",
        Description = "HIPAA-compliant high-throughput multi-tenant claims intake and schema validation microservice."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityDefinition("TenantHeader", new OpenApiSecurityScheme
    {
        Description = "Tenant Identifier Header (e.g. tenant-a, tenant-b).",
        Name = "X-Tenant-ID",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "TenantHeader" }
            },
            Array.Empty<string>()
        }
    });
});

// Configure Tenant Context
builder.Services.AddScoped<ITenantContext, TenantContext>();

// JSON Schema Validator
builder.Services.AddSingleton<IClaimValidator, JsonSchemaClaimValidator>();

// Persistence configuration (Azure Cosmos DB vs Local InMemory)
var cosmosConnection = builder.Configuration.GetConnectionString("CosmosDbConnection");
var useInMemoryStore = builder.Configuration.GetValue<bool>("Database:UseInMemory", false);

if (!useInMemoryStore && !string.IsNullOrWhiteSpace(cosmosConnection))
{
    var databaseName = builder.Configuration.GetValue<string>("CosmosDb:DatabaseName", "HealthcarePlatform");
    builder.Services.AddSingleton(sp =>
    {
        var cosmosClient = new CosmosClient(cosmosConnection, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });
        return cosmosClient;
    });

    builder.Services.AddScoped<IClaimRepository>(sp =>
    {
        var cosmosClient = sp.GetRequiredService<CosmosClient>();
        var logger = sp.GetRequiredService<ILogger<CosmosClaimRepository>>();
        return new CosmosClaimRepository(cosmosClient, databaseName, logger);
    });
}
else
{
    builder.Services.AddSingleton<IClaimRepository, InMemoryClaimRepository>();
}

// Event Publisher configuration (Azure Service Bus vs InMemory)
var eventBusProvider = builder.Configuration.GetValue<string>("EventBus:Provider", "InMemory");
var serviceBusConnection = builder.Configuration.GetConnectionString("ServiceBusConnection");

if (eventBusProvider.Equals("ServiceBus", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(serviceBusConnection))
{
    builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection));
    builder.Services.AddScoped<IEventPublisher, AzureServiceBusPublisher>();
}
else
{
    builder.Services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
}

// Application Services & Consumers
builder.Services.AddScoped<IClaimsService, ClaimsIngestionManager>();
builder.Services.AddHostedService<MemberEnrolledConsumerService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Claims Ingestion API v1"));
}

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseRouting();
app.MapControllers();

app.Run();
