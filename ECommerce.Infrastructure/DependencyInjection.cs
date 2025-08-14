using ECommerce.Application.Abstractions;
using ECommerce.Application.Orders;
using ECommerce.Infrastructure.Cache;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg, bool addMassTransit = true, bool addApplicationServices = true)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(cfg.GetConnectionString("Sql")));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(cfg["Redis:Connection"]!));
        services.AddSingleton<ICacheService, RedisCacheService>();

        if (addMassTransit)
        {
            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((ctx, mq) =>
                {
                    var host = cfg["RabbitMq:Host"] ?? "localhost";
                    var port = cfg.GetValue<int?>("RabbitMq:Port") ?? 5672;
                    var user = cfg["RabbitMq:Username"] ?? "guest";
                    var pass = cfg["RabbitMq:Password"] ?? "guest";

                    mq.Host(host, (ushort)port, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });
                });
            });
            services.AddScoped<IEventBus, MassTransitEventBus>();
        }

        if (addApplicationServices)
            services.AddScoped<IOrderService, OrderService>();

        return services;
    }
}
