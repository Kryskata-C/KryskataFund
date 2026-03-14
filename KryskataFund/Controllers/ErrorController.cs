using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    return View("NotFound");
                case 403:
                    return View("Forbidden");
                default:
                    return View("InternalError");
            }
        }

        [Route("Error/NotFound")]
        public IActionResult NotFound()
        {
            Response.StatusCode = 404;
            return View();
        }

        [Route("Error/Forbidden")]
        public IActionResult Forbidden()
        {
            Response.StatusCode = 403;
            return View();
        }

        [Route("Error/InternalError")]
        public IActionResult InternalError()
        {
            Response.StatusCode = 500;
            return View();
        }
    }
}
