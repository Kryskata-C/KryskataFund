using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KryskataFund.Models;
using KryskataFund.Data;
using KryskataFund.Constants;
using KryskataFund.Filters;
using Ganss.Xss;

namespace KryskataFund.Controllers
{
    public class FundsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;


        public FundsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        private bool IsCreatorOrCollaborator(int fundCreatorId, int userId, int fundId)
        {
            if (fundCreatorId == userId) return true;
            return _context.FundCollaborators.Any(c => c.FundId == fundId && c.UserId == userId);
        }

        [RequireSignIn]
        public IActionResult Create()
        {
            return View(new CreateFundViewModel());
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> Create(CreateFundViewModel model)
        {
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
                // Validate file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ImageFile", "Only image files (jpg, jpeg, png, gif, webp) are allowed.");
                    return View(model);
                }

                // Validate file size (max 5MB)
                if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ImageFile", "File size cannot exceed 5MB.");
                    return View(model);
                }

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

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var userEmail = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "Anonymous";

            var fund = new Fund
            {
                Title = model.Title,
                Description = new HtmlSanitizer().Sanitize(model.Description),
                Category = model.Category,
                GoalAmount = model.GoalAmount,
                RaisedAmount = 0,
                SupportersCount = 0,
                CreatorId = userId,
                CreatorName = "@" + userEmail.Split('@')[0],
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(model.DurationDays),
                CategoryColor = Constants.CategoryColors.GetColor(model.Category)
            };

            _context.Funds.Add(fund);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = fund.Id });
        }

        public IActionResult Embed(int id)
        {
            var fund = _context.Funds.FirstOrDefault(f => f.Id == id);

            if (fund == null)
            {
                return NotFound();
            }

            return View(fund);
        }

        public IActionResult Details(int id)
        {
            var fund = _context.Funds.FirstOrDefault(f => f.Id == id);

            if (fund == null)
            {
                return NotFound();
            }

            ViewBag.IsSignedIn = HttpContext.Session.GetString(SessionKeys.IsSignedIn) == "true";

            // Check if current user is the creator or collaborator
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            ViewBag.IsCreator = IsCreatorOrCollaborator(fund.CreatorId, userId, id);
            ViewBag.IsOriginalCreator = fund.CreatorId == userId;

            // Load collaborators with user emails
            var collaborators = _context.FundCollaborators
                .Where(c => c.FundId == id)
                .Join(_context.Users, c => c.UserId, u => u.Id, (c, u) => new { c.Id, c.UserId, c.Role, c.AddedAt, u.Email })
                .ToList();
            ViewBag.Collaborators = collaborators;

            // Get all donations for this fund (for pagination)
            var recentDonations = _context.Donations
                .Where(d => d.FundId == id)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            ViewBag.RecentDonations = recentDonations;

            // Get all updates for this fund
            var updates = _context.FundUpdates
                .Where(u => u.FundId == id)
                .OrderByDescending(u => u.CreatedAt)
                .ToList();

            ViewBag.FundUpdates = updates;

            // Get all milestones for this fund
            ViewBag.Milestones = _context.FundMilestones
                .Where(m => m.FundId == id)
                .OrderBy(m => m.TargetAmount)
                .ToList();

            // Check for existing deadline extension
            var extension = _context.DeadlineExtensions
                .FirstOrDefault(e => e.FundId == id);
            ViewBag.DeadlineExtension = extension;

            // Recurring donations count
            ViewBag.RecurringCount = _context.RecurringDonations
                .Count(r => r.FundId == id && r.IsActive);

            // Check if current user has an active recurring donation
            ViewBag.HasRecurring = userId > 0 && _context.RecurringDonations
                .Any(r => r.FundId == id && r.UserId == userId && r.IsActive);

            // Get user's active recurring donation amount for this fund
            var userRecurring = _context.RecurringDonations
                .FirstOrDefault(r => r.FundId == id && r.UserId == userId && r.IsActive);
            ViewBag.RecurringAmount = userRecurring?.Amount ?? 0;

            return View(fund);
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> RequestExtension(int fundId, int extensionDays, string reason)
        {
            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Only the creator can request an extension
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can request an extension" });
            }

            // Validate extension days
            if (extensionDays < 1 || extensionDays > 30)
            {
                return Json(new { success = false, message = "Extension must be between 1 and 30 days" });
            }

            // Validate reason
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            {
                return Json(new { success = false, message = "Please provide a reason (max 500 characters)" });
            }

            // Check fund has <= 7 days left OR ended within last 7 days
            var daysLeft = (fund.EndDate - DateTime.UtcNow).TotalDays;
            if (daysLeft > 7)
            {
                return Json(new { success = false, message = "Extensions can only be requested when there are 7 or fewer days left" });
            }
            if (daysLeft < -7)
            {
                return Json(new { success = false, message = "This fund ended more than 7 days ago and can no longer be extended" });
            }

            // Check no existing extension
            var existingExtension = _context.DeadlineExtensions.Any(e => e.FundId == fundId);
            if (existingExtension)
            {
                return Json(new { success = false, message = "This fund has already been extended once" });
            }

            var extension = new DeadlineExtension
            {
                FundId = fundId,
                OriginalEndDate = fund.EndDate,
                NewEndDate = fund.EndDate.AddDays(extensionDays),
                ExtensionDays = extensionDays,
                Reason = reason,
                RequestedAt = DateTime.UtcNow
            };

            _context.DeadlineExtensions.Add(extension);
            fund.EndDate = extension.NewEndDate;
            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                message = $"Deadline extended by {extensionDays} days!",
                newEndDate = fund.EndDate.ToString("MMM d, yyyy"),
                daysLeft = fund.DaysLeft,
                extensionDays = extensionDays
            });
        }



        [HttpGet]
        [RequireSignIn]
        public IActionResult GetRecentContacts()
        {
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");

            var recentContacts = _context.Messages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.SentAt)
                .Select(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .Take(5)
                .ToList();

            var contacts = _context.Users
                .Where(u => recentContacts.Contains(u.Id))
                .Select(u => new { id = u.Id, email = u.Email })
                .ToList()
                .OrderBy(u => recentContacts.IndexOf(u.id))
                .ToList();

            return Json(contacts);
        }


    }
}
