namespace ECommerce.Application.Orders;

public interface IOrderService
{
    Task<Guid> PlaceOrderAsync(PlaceOrderDto dto, CancellationToken ct);
    Task<IReadOnlyList<OrderViewModel>> GetOrdersOfUserAsync(string userId, CancellationToken ct);
}
