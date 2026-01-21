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

        public IActionResult Profile()
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return RedirectToAction("SignIn", new { returnUrl = "/Account/Profile" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                return RedirectToAction("SignOut");
            }

            // Get user's created funds
            var createdFunds = _context.Funds
                .Where(f => f.CreatorId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToList();

            // Get user's donations
            var donations = _context.Donations
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            // Calculate stats
            var totalRaised = createdFunds.Sum(f => f.RaisedAmount);
            var totalDonated = donations.Sum(d => d.Amount);
            var totalSupporters = createdFunds.Sum(f => f.SupportersCount);

            ViewBag.User = user;
            ViewBag.CreatedFunds = createdFunds;
            ViewBag.Donations = donations;
            ViewBag.TotalRaised = totalRaised;
            ViewBag.TotalDonated = totalDonated;
            ViewBag.TotalSupporters = totalSupporters;

            return View();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
