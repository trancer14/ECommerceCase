using ECommerce.Application.Abstractions;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    public Task AddAsync(Order order, CancellationToken ct = default)
        => _db.Orders.AddAsync(order, ct).AsTask();

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)!;

    public async Task<IReadOnlyList<Order>> GetByUserAsync(string userId, CancellationToken ct = default)
        => await _db.Orders.Where(o => o.UserId == userId)
                           .OrderByDescending(o => o.CreatedAt)
                           .ToListAsync(ct);
}
