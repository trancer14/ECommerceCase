using ECommerce.Infrastructure;               
using ECommerce.Worker.Consumers;              
using ECommerce.Shared.Contracts;              
using MassTransit;
using Microsoft.Extensions.Configuration;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Services.AddLogging(lb => lb.AddSerilog());

builder.Services.AddInfrastructure(builder.Configuration, addMassTransit: false,addApplicationServices:false);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration.GetValue<string>("RabbitMq:Host") ?? "localhost";
        var port = builder.Configuration.GetValue<int>("RabbitMq:Port", 5672);
        var user = builder.Configuration.GetValue<string>("RabbitMq:Username") ?? "guest";
        var pass = builder.Configuration.GetValue<string>("RabbitMq:Password") ?? "guest";

        cfg.Host(host, (ushort)port, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        cfg.ReceiveEndpoint("order-placed", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind<IOrderPlaced>();
            e.ConfigureConsumer<OrderPlacedConsumer>(ctx);
        });
    });
});

await builder.Build().RunAsync();
