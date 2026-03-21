using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KryskataFund.Constants;

namespace KryskataFund.Filters
{
    public class RequireSignInAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Session.GetString(SessionKeys.IsSignedIn) != "true")
            {
                context.Result = new RedirectToActionResult("SignIn", "Account", null);
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
