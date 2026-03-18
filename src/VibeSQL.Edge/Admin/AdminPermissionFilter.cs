using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Admin;

public class AdminPermissionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Items[EdgeContextKeys.PermissionLevel] is not VibePermissionLevel level
            || level < VibePermissionLevel.Admin)
        {
            context.Result = new JsonResult(new
            {
                success = false,
                error = new { code = "ADMIN_REQUIRED", message = "Admin permission required" }
            })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
