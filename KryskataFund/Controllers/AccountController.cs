using Microsoft.AspNetCore.Mvc;
using KryskataFund.Data;
using KryskataFund.Models;
using System.Security.Cryptography;
using System.Text;

namespace KryskataFund.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult SignIn(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public IActionResult SignIn(string email, string password, string? returnUrl = null)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null)
            {
                ViewData["Error"] = "Invalid email or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var passwordHash = HashPassword(password);
            if (user.PasswordHash != passwordHash)
            {
                ViewData["Error"] = "Invalid email or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            HttpContext.Session.SetString("IsSignedIn", "true");
            HttpContext.Session.SetString("UserEmail", email);
            HttpContext.Session.SetString("UserId", user.Id.ToString());

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult SignUp(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public IActionResult SignUp(SignUpViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if email already exists
            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "An account with this email already exists");
                return View(model);
            }

            // Create new user
            var user = new User
            {
                Email = model.Email,
                PasswordHash = HashPassword(model.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            // Sign in the user
            HttpContext.Session.SetString("IsSignedIn", "true");
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserId", user.Id.ToString());

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        public new IActionResult SignOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
