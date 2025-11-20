using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Controllers
{
    public class FundsController : Controller
    {
        public IActionResult Create()
        {
            return View();
        }
    }
}
