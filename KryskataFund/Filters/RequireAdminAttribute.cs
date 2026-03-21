using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KryskataFund.Constants;

namespace KryskataFund.Filters
{
    public class RequireAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!string.Equals(context.HttpContext.Session.GetString(SessionKeys.IsAdmin), "true", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToActionResult("Forbidden", "Error", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
