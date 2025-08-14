
namespace ECommerce.Application.Abstractions
{
    public interface IEventBus
    {
        Task PublishOrderPlacedAsync(object message, CancellationToken ct = default);
    }
}
