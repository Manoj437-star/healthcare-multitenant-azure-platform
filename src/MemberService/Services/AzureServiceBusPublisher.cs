using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace MemberService.Services;

public class AzureServiceBusPublisher : IEventPublisher
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<AzureServiceBusPublisher> _logger;

    public AzureServiceBusPublisher(ServiceBusClient client, ILogger<AzureServiceBusPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string topicName, T eventPayload, string tenantId, string correlationId, CancellationToken cancellationToken = default) where T : class
    {
        var sender = _client.CreateSender(topicName);
        var json = JsonSerializer.Serialize(eventPayload);
        var message = new ServiceBusMessage(BinaryData.FromString(json))
        {
            ContentType = "application/json",
            CorrelationId = correlationId,
            Subject = typeof(T).Name,
            MessageId = Guid.NewGuid().ToString()
        };

        // User properties for tenant routing and filter rules
        message.ApplicationProperties["TenantId"] = tenantId;
        message.ApplicationProperties["CorrelationId"] = correlationId;
        message.ApplicationProperties["EventType"] = typeof(T).Name;
        message.ApplicationProperties["TimestampUtc"] = DateTime.UtcNow.ToString("o");

        _logger.LogInformation("Publishing event {EventType} to Service Bus topic {TopicName} for tenant {TenantId}. CorrelationId: {CorrelationId}",
            typeof(T).Name, topicName, tenantId, correlationId);

        await sender.SendMessageAsync(message, cancellationToken);
    }
}

public class InMemoryEventPublisher : IEventPublisher
{
    private readonly ILogger<InMemoryEventPublisher> _logger;
    public static readonly List<(string Topic, string TenantId, string CorrelationId, object Payload)> PublishedEvents = new();

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(string topicName, T eventPayload, string tenantId, string correlationId, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogInformation("[LOCAL DEV EVENT BUS] Published {EventType} to topic {TopicName} for tenant {TenantId}. CorrelationId: {CorrelationId}",
            typeof(T).Name, topicName, tenantId, correlationId);

        lock (PublishedEvents)
        {
            PublishedEvents.Add((topicName, tenantId, correlationId, eventPayload));
        }

        return Task.CompletedTask;
    }
}
