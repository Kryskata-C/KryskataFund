using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    public class FundsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private static readonly Dictionary<string, string> CategoryColors = new()
        {
            { "Education", "#4ade80" },
            { "Health", "#f97316" },
            { "Animals", "#22d3ee" },
            { "Creative", "#a855f7" },
            { "Dreams", "#facc15" },
            { "Just for fun", "#ef4444" },
            { "Technology", "#3b82f6" },
            { "Community", "#ec4899" }
        };

        public FundsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public IActionResult Create()
        {
            // Must be signed in to create a fund
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/Funds/Create" });
            }

            return View(new CreateFundViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateFundViewModel model)
        {
            // Must be signed in
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/Funds/Create" });
            }

            // Need either URL or file
            if (string.IsNullOrEmpty(model.ImageUrl) && model.ImageFile == null)
            {
                ModelState.AddModelError("", "Please provide a cover image URL or upload a file");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? imageUrl = model.ImageUrl;

            // Handle file upload
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                imageUrl = "/uploads/" + uniqueFileName;
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? "Anonymous";

            var fund = new Fund
            {
                Title = model.Title,
                Description = model.Description,
                Category = model.Category,
                GoalAmount = model.GoalAmount,
                RaisedAmount = 0,
                SupportersCount = 0,
                CreatorId = userId,
                CreatorName = "@" + userEmail.Split('@')[0],
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(model.DurationDays),
                CategoryColor = CategoryColors.GetValueOrDefault(model.Category, "#4ade80")
            };

            _context.Funds.Add(fund);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = fund.Id });
        }

        public IActionResult Details(int id)
        {
            var fund = _context.Funds.FirstOrDefault(f => f.Id == id);

            if (fund == null)
            {
                return NotFound();
            }

            ViewBag.IsSignedIn = HttpContext.Session.GetString("IsSignedIn") == "true";

            // Get all donations for this fund (for pagination)
            var recentDonations = _context.Donations
                .Where(d => d.FundId == id)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            ViewBag.RecentDonations = recentDonations;

            return View(fund);
        }

        public IActionResult Donate(int id, int amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return RedirectToAction("SignIn", "Account", new { returnUrl = $"/Funds/Donate?id={id}&amount={amount}" });
            }

            var fund = _context.Funds.FirstOrDefault(f => f.Id == id);

            if (fund == null)
            {
                return NotFound();
            }

            ViewBag.Amount = amount;
            return View(fund);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessDonation(int fundId, decimal amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in to donate" });
            }

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? "Anonymous";
            var donorName = "@" + userEmail.Split('@')[0];

            // Create the donation record
            var donation = new Donation
            {
                FundId = fundId,
                UserId = userId,
                DonorName = donorName,
                Amount = amount,
                CreatedAt = DateTime.UtcNow
            };

            _context.Donations.Add(donation);

            // Update the fund's raised amount and supporters count
            fund.RaisedAmount += amount;
            fund.SupportersCount += 1;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donation successful!" });
        }
    }
}
