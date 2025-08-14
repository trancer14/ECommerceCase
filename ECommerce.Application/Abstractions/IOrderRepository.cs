using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order, CancellationToken ct = default);
        Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Order>> GetByUserAsync(string userId, CancellationToken ct = default);
    }
}
