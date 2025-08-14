using ECommerce.Api.Middleware;
using ECommerce.Application.Orders;
using ECommerce.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderDto dto, CancellationToken ct)
    {
        var id = await orders.PlaceOrderAsync(dto, ct);
        var cid = HttpContext.GetCorrelationId();
        return Accepted(ApiResponse<object>.Ok(new { id }, cid));
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetByUser(string userId, CancellationToken ct)
    {
        var list = await orders.GetOrdersOfUserAsync(userId, ct);
        var cid = HttpContext.GetCorrelationId();
        return Ok(ApiResponse<IReadOnlyList<OrderViewModel>>.Ok(list, cid));
    }
}
