namespace MemberService.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(string topicName, T eventPayload, string tenantId, string correlationId, CancellationToken cancellationToken = default) where T : class;
}
