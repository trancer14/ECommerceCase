using ECommerce.Application.Abstractions;
using ECommerce.Application.Orders;
using ECommerce.Domain.Entities;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerce.Tests.Services;

public class OrderServiceEdgeTests
{
    private readonly Mock<IOrderRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IEventBus> _bus = new();
    private readonly Mock<ICacheService> _cache = new();

    [Fact]
    public async Task PlaceOrder_ShouldThrow_WhenQuantityIsZeroOrNegative()
    {
        var svc = new OrderService(_repo.Object, _uow.Object, _bus.Object, _cache.Object);

        var act = async () => await svc.PlaceOrderAsync(
            new PlaceOrderDto("u1", "p1", 0, PaymentMethod.CreditCard), default);

        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*quantity*");

        _repo.VerifyNoOtherCalls();
        _uow.VerifyNoOtherCalls();
        _bus.VerifyNoOtherCalls();
        _cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PlaceOrder_ShouldInvalidate_OnlyThatUserCache()
    {
        var svc = new OrderService(_repo.Object, _uow.Object, _bus.Object, _cache.Object);

        await svc.PlaceOrderAsync(new PlaceOrderDto("u1", "p1", 1, PaymentMethod.CreditCard), default);
        await svc.PlaceOrderAsync(new PlaceOrderDto("u2", "p2", 1, PaymentMethod.CreditCard), default);

        _cache.Verify(c => c.RemoveAsync("orders:u1"), Times.Once);
        _cache.Verify(c => c.RemoveAsync("orders:u2"), Times.Once);
        _cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetOrdersOfUser_ShouldReturnFromCache_OnSecondCall()
    {
        // 1) İlk çağrıda cache miss
        _cache.Setup(c => c.GetStringAsync("orders:u1"))
              .ReturnsAsync((string?)null);

        _repo.Setup(r => r.GetByUserAsync("u1", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Order> { new("u1", "p1", 2, PaymentMethod.BankTransfer) });

        var svc = new OrderService(_repo.Object, _uow.Object, _bus.Object, _cache.Object);

        var first = await svc.GetOrdersOfUserAsync("u1", default);

        // 2) İkinci çağrıda cache hit
        var serialized = System.Text.Json.JsonSerializer.Serialize(first);
        _cache.Setup(c => c.GetStringAsync("orders:u1")).ReturnsAsync(serialized);

        var second = await svc.GetOrdersOfUserAsync("u1", default);

        // Repo sadece bir kez çalıştı mı?
        _repo.Verify(r => r.GetByUserAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        second.Should().HaveCount(1);
    }
}
