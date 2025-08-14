using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Api;
using ECommerce.Application.Abstractions;
using ECommerce.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Api;

public class CustomWebApplicationFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(WebHostDefaults.ContentRootKey, GetApiProjectPath());

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<HealthCheckServiceOptions>(opts =>
            {
                var toRemove = opts.Registrations
                    .Where(r => string.Equals(r.Name, "masstransit-bus", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var reg in toRemove) opts.Registrations.Remove(reg);
            });

            var hostedToRemove = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType != null
                            && d.ImplementationType.FullName!.Contains("MassTransit", StringComparison.Ordinal))
                .ToList();
            foreach (var d in hostedToRemove) services.Remove(d);

            services.RemoveAll<IEventBus>();
            services.AddSingleton<IEventBus, NoopEventBus>();

            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));

            const string DbName = "api-e2e-db";

            services.AddEntityFrameworkInMemoryDatabase();
            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseInMemoryDatabase(DbName);
            });

            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, TestCacheService>();

            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
                o.DefaultScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    private static string GetApiProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var apiDir = Path.Combine(dir.FullName, "ECommerce.Api");
            var csproj = Path.Combine(apiDir, "ECommerce.Api.csproj");
            if (File.Exists(csproj)) return apiDir;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}

public sealed class NoopEventBus : IEventBus
{
    public Task PublishOrderPlacedAsync(object message, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var uid = Context.Request.Headers.TryGetValue("X-Test-UserId", out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : "test";

        var claims = new[]
        {
        new Claim(ClaimTypes.NameIdentifier, uid),
        new Claim(ClaimTypes.Name, uid)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class TestCacheService : ICacheService
{
    private readonly Dictionary<string, (string Value, DateTimeOffset? Expire)> _store = new();

    public Task<string?> GetStringAsync(string key)
    {
        if (_store.TryGetValue(key, out var t))
        {
            if (t.Expire is null || t.Expire > DateTimeOffset.UtcNow)
                return Task.FromResult<string?>(t.Value);
            _store.Remove(key);
        }
        return Task.FromResult<string?>(null);
    }

    public Task SetStringAsync(string key, string value, TimeSpan ttl)
    {
        _store[key] = (value, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
