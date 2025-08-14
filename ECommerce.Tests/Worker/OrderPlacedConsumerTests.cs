using ECommerce.Infrastructure.Persistence;
using ECommerce.Shared.Contracts;
using ECommerce.Worker.Consumers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using Xunit;
using ECommerce.Domain.Entities;
using System.Linq;

namespace ECommerce.Tests.Worker;

public class OrderPlacedConsumerTests
{
    [Fact]
    public async Task Consumer_Should_Update_Order_And_Write_Redis_Log()
    {
        var services = new ServiceCollection();

       
        var dbName = "worker-db";
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName));

       
        var fakeDb = new Mock<IDatabase>(MockBehavior.Loose);
       
        fakeDb.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);
        
        fakeDb.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
              .ReturnsAsync(true);

        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(fakeDb.Object);
        services.AddSingleton<IConnectionMultiplexer>(mux.Object);

      
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderPlacedConsumer>();
            x.UsingInMemory((ctx, cfg) =>
            {
                cfg.ReceiveEndpoint("order-placed", e =>
                {
                    e.ConfigureConsumer<OrderPlacedConsumer>(ctx);
                });
            });
        });

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Guid orderId;
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = new Domain.Entities.Order("u1", "p1", 1, PaymentMethod.CreditCard);
            orderId = pending.Id;                   
            db.Orders.Add(pending);
            await db.SaveChangesAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            /
            await harness.Bus.Publish<IOrderPlaced>(new
            {
                OrderId = orderId,
                UserId = "u1",
                ProductId = "p1",
                Quantity = 1,
                PaymentMethod = 0,
                CreatedAt = DateTime.UtcNow
            });

            
            (await harness.Consumed.Any<IOrderPlaced>()).Should().BeTrue();

            
            await using (var assertScope = provider.CreateAsyncScope())
            {
                var db = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

                var updated = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
                updated.Should().NotBeNull("seed ettiğimiz order aynı InMemory store'da olmalı");
                updated!.Status.Should().Be("Processed");
                updated.ProcessedAt.Should().NotBeNull();
            }

          
            var expectedKey = $"order:{orderId}:processedAt";
            try
            {
                fakeDb.Verify(d => d.StringSetAsync(
                    It.Is<RedisKey>(k => k.ToString() == expectedKey),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()), Times.Once);
            }
            catch (MockException)
            {
                fakeDb.Verify(d => d.StringSetAsync(
                    It.Is<RedisKey>(k => k.ToString() == expectedKey),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()), Times.Once);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }


}
