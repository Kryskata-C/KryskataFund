using Microsoft.AspNetCore.Mvc;

namespace KryskataFUnd.Controllers
{
    public class FundsController : Controller
    {
        public IActionResult Create()
        {
            return View();
        }
    }
}
