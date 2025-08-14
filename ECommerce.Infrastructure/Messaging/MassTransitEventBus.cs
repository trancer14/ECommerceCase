using ECommerce.Application.Abstractions;
using ECommerce.Shared.Contracts;
using MassTransit;

namespace ECommerce.Infrastructure.Messaging;

public class MassTransitEventBus(IPublishEndpoint publisher) : IEventBus
{
    public Task PublishOrderPlacedAsync(object message, CancellationToken ct = default)
        => publisher.Publish<IOrderPlaced>(message, ct);
}
