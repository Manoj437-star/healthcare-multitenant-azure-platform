using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ClaimsIngestionService.Data;
using ClaimsIngestionService.Domain;
using ClaimsIngestionService.DTOs;

namespace ClaimsIngestionService.Services;

public class MemberEnrolledConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MemberEnrolledConsumerService> _logger;
    private ServiceBusProcessor? _processor;

    public MemberEnrolledConsumerService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MemberEnrolledConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var provider = _configuration.GetValue<string>("EventBus:Provider", "InMemory");
        var connectionString = _configuration.GetConnectionString("ServiceBusConnection");

        if (provider.Equals("ServiceBus", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogInformation("Starting Service Bus Processor on topic 'member-events', subscription 'sub-claims-ingestion-member-enrolled'...");
            var client = new ServiceBusClient(connectionString);
            _processor = client.CreateProcessor("member-events", "sub-claims-ingestion-member-enrolled", new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 10,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });

            _processor.ProcessMessageAsync += MessageHandler;
            _processor.ProcessErrorAsync += ErrorHandler;

            await _processor.StartProcessingAsync(stoppingToken);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful stop
            }
            finally
            {
                if (_processor != null)
                {
                    await _processor.StopProcessingAsync();
                    await _processor.DisposeAsync();
                }
            }
        }
        else
        {
            _logger.LogInformation("ServiceBus connection not provided. Running in Local Simulation Consumer mode.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var correlationId = args.Message.CorrelationId ?? Guid.NewGuid().ToString();
        var tenantId = args.Message.ApplicationProperties.TryGetValue("TenantId", out var t) ? t.ToString() ?? "unknown" : "unknown";

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = tenantId,
            ["CorrelationId"] = correlationId
        }))
        {
            try
            {
                var body = args.Message.Body.ToString();
                var enrolledEvent = JsonSerializer.Deserialize<MemberEnrolledEvent>(body);

                if (enrolledEvent == null || string.IsNullOrWhiteSpace(enrolledEvent.MemberId))
                {
                    _logger.LogWarning("Invalid MemberEnrolled event body. Dead-lettering message {MessageId}.", args.Message.MessageId);
                    await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Payload could not be parsed to MemberEnrolledEvent");
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IClaimRepository>();

                // Idempotency check: record event ID
                var isFirstTime = await repository.TryRecordIdempotentOperationAsync(
                    enrolledEvent.TenantId,
                    $"event_{enrolledEvent.EventId}",
                    args.CancellationToken);

                if (!isFirstTime)
                {
                    _logger.LogInformation("Event {EventId} already processed for tenant {TenantId}. Acknowledging message.",
                        enrolledEvent.EventId, enrolledEvent.TenantId);
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }

                // Update local eligibility cache
                var cacheItem = new EnrolledMemberCache
                {
                    TenantId = enrolledEvent.TenantId,
                    MemberId = enrolledEvent.MemberId,
                    PlanCode = enrolledEvent.PlanCode,
                    Status = enrolledEvent.Status,
                    CoverageStartDate = enrolledEvent.CoverageStartDate,
                    CachedAtUtc = DateTime.UtcNow
                };

                await repository.UpsertMemberCacheAsync(cacheItem, args.CancellationToken);

                _logger.LogInformation("Successfully cached eligibility for member {MemberId} for tenant {TenantId}.",
                    enrolledEvent.MemberId, enrolledEvent.TenantId);

                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process MemberEnrolled event {MessageId}. DeliveryCount: {DeliveryCount}.",
                    args.Message.MessageId, args.Message.DeliveryCount);

                if (args.Message.DeliveryCount >= 5)
                {
                    _logger.LogWarning("Message {MessageId} reached max delivery count. Moving to Dead Letter Queue.", args.Message.MessageId);
                    await args.DeadLetterMessageAsync(args.Message, "MaxRetriesExceeded", ex.Message);
                }
                else
                {
                    await args.AbandonMessageAsync(args.Message);
                }
            }
        }
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus Processor error on entity {EntityPath}: {ErrorSource}",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }
}
