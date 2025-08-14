using ECommerce.Infrastructure.Persistence;
using ECommerce.Shared.Contracts;  
using MassTransit;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ECommerce.Worker.Consumers;

public class OrderPlacedConsumer(
    AppDbContext db,
    IConnectionMultiplexer mux,
    ILogger<OrderPlacedConsumer> logger
) : IConsumer<IOrderPlaced>    
{
    public async Task Consume(ConsumeContext<IOrderPlaced> context)
    {
        var msg = context.Message;
        var orderId = msg.OrderId;

        logger.LogInformation("Received OrderPlaced: {OrderId}", orderId);
        await Task.Delay(Random.Shared.Next(500, 1500));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is not null)
        {
            order.MarkProcessed();
            await db.SaveChangesAsync();
        }

        await mux.GetDatabase()
                 .StringSetAsync($"order:{orderId}:processedAt", DateTime.UtcNow.ToString("O"));

        logger.LogInformation("Processed {OrderId}", orderId);
    }
}
