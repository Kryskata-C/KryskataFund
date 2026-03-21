using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KryskataFund.Constants;

namespace KryskataFund.Filters
{
    public class RequireAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Session.GetString(SessionKeys.IsAdmin) != "True")
            {
                context.Result = new RedirectToActionResult("Index", "Home", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
