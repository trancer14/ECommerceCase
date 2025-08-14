using Serilog.Context;

namespace ECommerce.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[HeaderName] = correlationId;

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdHttpExtensions
{
    private const string HeaderName = "X-Correlation-ID";

    public static string? GetCorrelationId(this HttpContext ctx)
        => (ctx.Items[HeaderName] as string) ?? ctx.Response.Headers[HeaderName].ToString();
}
