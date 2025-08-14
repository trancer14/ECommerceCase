using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Application.Abstractions;
using ECommerce.Application.Orders;
using ECommerce.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IEventBus> _bus = new();
    private readonly Mock<ICacheService> _cache = new();

    [Fact]
    public async Task PlaceOrder_Publishes_Event_And_Invalidates_Cache()
    {
        var svc = new OrderService(_repo.Object, _uow.Object, _bus.Object, _cache.Object);
        var dto = new PlaceOrderDto("u1", "p1", 2, PaymentMethod.CreditCard);

        var id = await svc.PlaceOrderAsync(dto, default);

        _repo.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _bus.Verify(b => b.PublishOrderPlacedAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync("orders:u1"), Times.Once);
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetOrdersOfUser_UsesCache_OnSecondCall()
    {
        _cache.Setup(c => c.GetStringAsync("orders:u1")).ReturnsAsync((string?)null);
        _repo.Setup(r => r.GetByUserAsync("u1", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Order> { new("u1", "p1", 1, PaymentMethod.BankTransfer) });

        var svc = new OrderService(_repo.Object, _uow.Object, _bus.Object, _cache.Object);

        var first = await svc.GetOrdersOfUserAsync("u1", default);

        _cache.Verify(c => c.SetStringAsync("orders:u1", It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);

        _cache.Setup(c => c.GetStringAsync("orders:u1"))
              .ReturnsAsync(JsonSerializer.Serialize(first));

        var second = await svc.GetOrdersOfUserAsync("u1", default);

        _repo.Verify(r => r.GetByUserAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        second.Should().HaveCount(1);
    }
}
