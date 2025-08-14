using ECommerce.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECommerce.Api.Filters;

public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (!ctx.ModelState.IsValid)
        {
            var cid = ctx.HttpContext.Response.Headers["X-Correlation-ID"].ToString();
            var errors = ctx.ModelState
                .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                    new ErrorItem("validation_error", e.ErrorMessage, kvp.Key)));
            var resp = ApiResponse<object>.Fail(errors, cid);
            ctx.Result = new BadRequestObjectResult(resp);
        }
    }
    public void OnActionExecuted(ActionExecutedContext ctx) { }
}
