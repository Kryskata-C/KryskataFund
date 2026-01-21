using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult SignIn(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public IActionResult SignIn(string email, string password, string? returnUrl = null)
        {
            // Mock sign in - just set session flag
            HttpContext.Session.SetString("IsSignedIn", "true");
            HttpContext.Session.SetString("UserEmail", email);

            // Redirect back to where they came from, or home
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult SignOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}
