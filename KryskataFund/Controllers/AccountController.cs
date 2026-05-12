using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Constants;
using KryskataFund.Filters;
using KryskataFund.Services;

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
        public async Task<IActionResult> SignIn(string email, string password, string? returnUrl = null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ViewData["Error"] = "Invalid email or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                ViewData["Error"] = "Invalid email or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Auto-upgrade legacy SHA256 hash to BCrypt
            if (!user.PasswordHash.StartsWith("$2"))
            {
                user.PasswordHash = PasswordHasher.HashPassword(password);
                await _context.SaveChangesAsync();
            }

            HttpContext.Session.SetString(SessionKeys.IsSignedIn, "true");
            HttpContext.Session.SetString(SessionKeys.UserEmail, email);
            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
            HttpContext.Session.SetString(SessionKeys.IsAdmin, user.IsAdmin.ToString());

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
        public async Task<IActionResult> SignUp(SignUpViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "An account with this email already exists");
                return View(model);
            }

            var user = new User
            {
                Email = model.Email,
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Sign in the user
            HttpContext.Session.SetString(SessionKeys.IsSignedIn, "true");
            HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email);
            HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());

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

        [RequireSignIn]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return RedirectToAction("SignOut");
            }

            var createdFunds = await _context.Funds
                .Where(f => f.CreatorId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var donations = await _context.Donations
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

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
            ViewBag.BuddyGlasses = user.BuddyGlasses;
            ViewBag.BuddyHat = user.BuddyHat;
            ViewBag.BuddyMask = user.BuddyMask;

            return View();
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> SaveBuddyCustomization(string? glasses, string? hat, string? mask)
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            user.BuddyGlasses = glasses;
            user.BuddyHat = hat;
            user.BuddyMask = mask;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public async Task<IActionResult> GetBuddyCustomization()
        {
            if (HttpContext.Session.GetString(SessionKeys.IsSignedIn) != "true")
            {
                return Json(new { glasses = (string?)null, hat = (string?)null, mask = (string?)null });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Json(new { glasses = (string?)null, hat = (string?)null, mask = (string?)null });
            }

            return Json(new { glasses = user.BuddyGlasses, hat = user.BuddyHat, mask = user.BuddyMask });
        }

        [RequireSignIn]
        public async Task<IActionResult> MyFunds()
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var myFunds = await _context.Funds
                .Where(f => f.CreatorId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var totalRaised = myFunds.Sum(f => f.RaisedAmount);
            var totalSupporters = myFunds.Sum(f => f.SupportersCount);

            ViewBag.MyFunds = myFunds;
            ViewBag.TotalRaised = totalRaised;
            ViewBag.TotalSupporters = totalSupporters;

            return View();
        }

        [RequireSignIn]
        public async Task<IActionResult> MyDonations()
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var myDonations = await _context.Donations
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            var funds = await _context.Funds.ToListAsync();
            var totalDonated = myDonations.Sum(d => d.Amount);
            var campaignsSupported = myDonations.Select(d => d.FundId).Distinct().Count();

            var recurringDonations = await _context.RecurringDonations
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.MyDonations = myDonations;
            ViewBag.Funds = funds;
            ViewBag.TotalDonated = totalDonated;
            ViewBag.CampaignsSupported = campaignsSupported;
            ViewBag.RecurringDonations = recurringDonations;

            return View();
        }

        [RequireSignIn]
        public async Task<IActionResult> Following()
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var followedFundIds = await _context.UserFollows
                .Where(f => f.UserId == userId)
                .Select(f => f.FundId)
                .ToListAsync();

            var followedFunds = await _context.Funds
                .Where(f => followedFundIds.Contains(f.Id))
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            ViewBag.FollowedFunds = followedFunds;
            ViewBag.FollowCount = followedFunds.Count;

            return View();
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> ToggleFollow(int fundId)
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var existingFollow = await _context.UserFollows
                .FirstOrDefaultAsync(f => f.UserId == userId && f.FundId == fundId);

            bool isNowFollowing;
            if (existingFollow != null)
            {
                _context.UserFollows.Remove(existingFollow);
                isNowFollowing = false;
            }
            else
            {
                _context.UserFollows.Add(new UserFollow
                {
                    UserId = userId,
                    FundId = fundId,
                    FollowedAt = DateTime.UtcNow
                });
                isNowFollowing = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isFollowing = isNowFollowing });
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> CancelRecurringDonation(int id)
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var recurring = await _context.RecurringDonations
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId && r.IsActive);

            if (recurring == null)
            {
                return Json(new { success = false, message = "Recurring donation not found" });
            }

            recurring.IsActive = false;
            recurring.CancelledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Recurring donation cancelled" });
        }
    }
}
