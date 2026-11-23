using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MemberService.Data;
using MemberService.Domain;
using MemberService.DTOs;

namespace MemberService.Services;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(2);
    private const int MaxBatchSize = 25;
    private const int MaxRetries = 5;

    public OutboxPublisherWorker(IServiceProvider serviceProvider, ILogger<OutboxPublisherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing outbox messages.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxPublisherWorker stopped.");
    }

    private async Task ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MemberDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // Disable global query filter so the worker can process outbox messages for all tenants
        var messages = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == "Pending" && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(MaxBatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} pending outbox messages to publish.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                if (message.EventType == "MemberEnrolled")
                {
                    var domainEvent = JsonSerializer.Deserialize<MemberEnrolledEvent>(message.Payload);
                    if (domainEvent != null)
                    {
                        await eventPublisher.PublishAsync(
                            "member-events",
                            domainEvent,
                            message.TenantId,
                            message.CorrelationId,
                            cancellationToken
                        );
                    }
                }

                message.Status = "Processed";
                message.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.ErrorMessage = ex.Message;
                if (message.RetryCount >= MaxRetries)
                {
                    message.Status = "Failed";
                    _logger.LogError(ex, "Outbox message {Id} exceeded max retries and is marked Failed.", message.Id);
                }
                else
                {
                    _logger.LogWarning(ex, "Failed to publish outbox message {Id}. Retry attempt {RetryCount}.", message.Id, message.RetryCount);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
