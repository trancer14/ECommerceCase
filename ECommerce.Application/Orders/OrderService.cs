using System.Text.Json;
using ECommerce.Application.Abstractions;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Orders;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IEventBus _bus;
    private readonly ICacheService _cache;

    public OrderService(IOrderRepository repo, IUnitOfWork uow, IEventBus bus, ICacheService cache)
    {
        _repo = repo; _uow = uow; _bus = bus; _cache = cache;
    }

    private static string CacheKey(string userId) => $"orders:{userId}";

    public async Task<Guid> PlaceOrderAsync(PlaceOrderDto dto, CancellationToken ct)
    {
        var order = new Order(dto.UserId, dto.ProductId, dto.Quantity, dto.PaymentMethod);
        await _repo.AddAsync(order, ct);
        await _uow.SaveChangesAsync(ct);

        await _bus.PublishOrderPlacedAsync(new
        {
            OrderId = order.Id,
            UserId = order.UserId,
            ProductId = order.ProductId,
            Quantity = order.Quantity,
            PaymentMethod = (int)order.PaymentMethod,
            CreatedAt = order.CreatedAt
        }, ct);


        await _cache.RemoveAsync(CacheKey(dto.UserId));

        return order.Id;
    }

    public async Task<IReadOnlyList<OrderViewModel>> GetOrdersOfUserAsync(string userId, CancellationToken ct)
    {
        var key = CacheKey(userId);
        var cached = await _cache.GetStringAsync(key);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<OrderViewModel>>(cached)!;
        }

        var orders = await _repo.GetByUserAsync(userId, ct);
        var list = orders.Select(o => new OrderViewModel(o.Id, o.ProductId, o.Quantity, o.PaymentMethod,
                                                         o.Status, o.CreatedAt, o.ProcessedAt))
                         .ToList();

        var json = JsonSerializer.Serialize(list);
        await _cache.SetStringAsync(key, json, TimeSpan.FromMinutes(2));
        return list;
    }
}
