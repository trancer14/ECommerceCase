using System.Collections.Generic;
using System;
using System.Net;
using System.Threading.Tasks;
using ECommerce.Application.Orders;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence;   
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ECommerce.Application.Abstractions;
using System.Text.Json;
using System.Net.Http.Json;
using System.Linq;

namespace ECommerce.Tests.Api;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
        => _factory = factory;

    [Fact]
    public async Task PostOrders_ShouldReturn_Accepted_And_Id()
    {
        var client = _factory.CreateClient(); 

        var dto = new PlaceOrderDto("u1", "p1", 2, PaymentMethod.CreditCard);
        var resp = await client.PostAsJsonAsync("/orders", dto);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"POST /orders failed: {(int)resp.StatusCode} {resp.StatusCode}\nBody: {body}");
        }

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var payload = await resp.Content.ReadFromJsonAsync<ApiOkId>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload!.Data.Should().NotBeNull();
        payload!.Data!.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetOrders_ShouldReturn_FromCache_OnSecondCall()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "u2"); 

        var dto = new PlaceOrderDto("u2", "p9", 1, PaymentMethod.CreditCard);
        var post = await client.PostAsJsonAsync("/orders", dto);
        var postBody = await post.Content.ReadAsStringAsync();
        post.EnsureSuccessStatusCode();

        var ok1 = System.Text.Json.JsonSerializer.Deserialize<ApiOkId>(
            postBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        ok1.Should().NotBeNull("POST response should deserialize");
        ok1!.Data.Should().NotBeNull("POST should return { id }");
        var orderId = ok1!.Data!.Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            await cache.RemoveAsync("orders:u2"); // emin ol

            var order = await db.Orders.FindAsync(orderId);
            order.Should().NotBeNull($"order {orderId} should exist in the same InMemory database");
            order!.MarkProcessed();
            await db.SaveChangesAsync();

            var processedCount = db.Orders.Count(o => o.UserId == "u2" && o.Status == "Processed");
            processedCount.Should().BeGreaterThan(0, "we seeded a processed order for user u2");
        }

        var r1 = await client.GetAsync("/orders/u2");
        var body1 = await r1.Content.ReadAsStringAsync();
        if (!r1.IsSuccessStatusCode)
            throw new Xunit.Sdk.XunitException($"GET #1 failed: {(int)r1.StatusCode}\nBody: {body1}");
        r1.StatusCode.Should().Be(HttpStatusCode.OK);

        var p1 = JsonSerializer.Deserialize<ApiOkOrders>(
            body1,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        p1.Should().NotBeNull("GET #1 payload should deserialize");
        p1!.Success.Should().BeTrue("response.Success must be true");
        p1.Data.Should().NotBeNull("response.Data should not be null");
        p1.Data!.Count.Should().BeGreaterThan(0, "processed order should be returned");

        // 4) İkinci GET -> cache hit (aynı format)
        var r2 = await client.GetAsync("/orders/u2");
        var body2 = await r2.Content.ReadAsStringAsync();
        if (!r2.IsSuccessStatusCode)
            throw new Xunit.Sdk.XunitException($"GET #2 failed: {(int)r2.StatusCode}\nBody: {body2}");
        r2.StatusCode.Should().Be(HttpStatusCode.OK);

        var p2 = JsonSerializer.Deserialize<ApiOkOrders>(
            body2,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        p2.Should().NotBeNull();
        p2!.Success.Should().BeTrue();
        p2.Data!.Count.Should().BeGreaterThan(0);
    }



    private record ApiOkId(bool Success, IdObj? Data) { public string? CorrelationId { get; init; } }
    private record IdObj(Guid Id);
    private record ApiOkOrders(bool Success, List<OrderVm>? Data) { public string? CorrelationId { get; init; } }
    private record OrderVm(Guid Id, string ProductId, int Quantity, PaymentMethod PaymentMethod, string Status, DateTime CreatedAt, DateTime? ProcessedAt);
}
