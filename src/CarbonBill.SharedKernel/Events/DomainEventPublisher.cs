using CarbonBill.SharedKernel.Domain;
using Microsoft.Extensions.Logging;

namespace CarbonBill.SharedKernel.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}


public record DocumentExtractedEvent(
    Guid DocumentId,
    Guid OrgId,
    int TierUsed,
    string? DetectedDocType,
    int FieldCount,
    DateTime OccurredOnUtc) : IDomainEvent;

public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
    Task PublishAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

public class InMemoryDomainEventPublisher(
    IServiceProvider serviceProvider,
    ILogger<InMemoryDomainEventPublisher> logger) : IDomainEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        logger.LogDebug("Publishing domain event: {EventType} at {OccurredOn}", domainEvent.GetType().Name, domainEvent.OccurredOnUtc);
        
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = (IEnumerable<object>)(serviceProvider.GetService(typeof(IEnumerable<>).MakeGenericType(handlerType)) ?? Array.Empty<object>());

        foreach (var handler in handlers)
        {
            var method = handlerType.GetMethod(nameof(IDomainEventHandler<TEvent>.HandleAsync));
            if (method != null)
            {
                var task = (Task?)method.Invoke(handler, [domainEvent, cancellationToken]);
                if (task != null)
                {
                    await task;
                }
            }
        }
    }

    public async Task PublishAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent, cancellationToken);
        }
    }
}
