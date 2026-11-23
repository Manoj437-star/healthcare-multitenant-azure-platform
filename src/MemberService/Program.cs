using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using MemberService.Data;
using MemberService.Middleware;
using MemberService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [Tenant: {TenantId}] [Corr: {CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Healthcare Multi-Tenant Platform - Member Service",
        Version = "v1",
        Description = "HIPAA-compliant multi-tenant member enrollment and eligibility microservice."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
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

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("MemberDbConnection");
var useInMemoryDb = builder.Configuration.GetValue<bool>("Database:UseInMemory", false);

builder.Services.AddDbContext<MemberDbContext>((sp, options) =>
{
    if (useInMemoryDb || string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("MemberDb_InMemory");
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    }
});

// Configure Event Publisher (Azure Service Bus vs In-Memory Local)
var eventBusProvider = builder.Configuration.GetValue<string>("EventBus:Provider", "InMemory");
if (eventBusProvider.Equals("ServiceBus", StringComparison.OrdinalIgnoreCase))
{
    var serviceBusConn = builder.Configuration.GetConnectionString("ServiceBusConnection");
    if (!string.IsNullOrWhiteSpace(serviceBusConn))
    {
        builder.Services.AddSingleton(new ServiceBusClient(serviceBusConn));
        builder.Services.AddScoped<IEventPublisher, AzureServiceBusPublisher>();
    }
    else
    {
        builder.Services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
    }
}
else
{
    builder.Services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
}

// Register Application Services & Background Worker
builder.Services.AddScoped<IMemberService, MemberManagementService>();
builder.Services.AddHostedService<OutboxPublisherWorker>();

var app = builder.Build();

// Ensure DB Created & Migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MemberDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }
}

// Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Member Service API v1"));
}

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseRouting();
app.MapControllers();

app.Run();
