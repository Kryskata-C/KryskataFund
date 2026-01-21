using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult SignIn()
        {
            return View();
        }
    }
}
