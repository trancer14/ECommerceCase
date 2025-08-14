using System.Net;
using ECommerce.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException vex) 
        {
            await Write(ctx, HttpStatusCode.BadRequest, vex.Errors.Select(e =>
                new ErrorItem("validation_error", e.ErrorMessage, e.PropertyName)));
        }
        catch (ArgumentException aex) 
        {
            await Write(ctx, HttpStatusCode.BadRequest, [new("domain_validation_failed", aex.Message)]);
        }
        catch (DbUpdateException dbx)
        {
            await Write(ctx, HttpStatusCode.Conflict, [new("db_update_error", dbx.InnerException?.Message ?? dbx.Message)]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await Write(ctx, HttpStatusCode.InternalServerError, [new("internal_error", "Beklenmeyen bir hata oluştu")]);
        }
    }

    private static async Task Write(HttpContext ctx, HttpStatusCode code, IEnumerable<ErrorItem> errors)
    {
        var cid = ctx.Response.Headers["X-Correlation-ID"].ToString();
        var resp = ApiResponse<object>.Fail(errors, cid);
        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = (int)code;
        await ctx.Response.WriteAsJsonAsync(resp);
    }
}
