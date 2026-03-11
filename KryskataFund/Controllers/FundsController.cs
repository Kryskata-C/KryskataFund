using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    public class FundsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

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

        public FundsController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
        }

        private bool IsCreatorOrCollaborator(int fundCreatorId, int userId, int fundId)
        {
            if (fundCreatorId == userId) return true;
            return _context.FundCollaborators.Any(c => c.FundId == fundId && c.UserId == userId);
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

            ViewBag.IsSignedIn = HttpContext.Session.GetString("IsSignedIn") == "true";

            // Check if current user is the creator or collaborator
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
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
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
            return View(fund);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(int fundId, decimal amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
                return Json(new { success = false, message = "Please sign in" });

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
                return Json(new { success = false, message = "Fund not found" });

            var domain = $"{Request.Scheme}://{Request.Host}";

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                {
                    new()
                    {
                        PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(amount * 100),
                            Currency = "eur",
                            ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Donation to: {fund.Title}",
                                Description = $"Supporting {fund.CreatorName}'s campaign"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"{domain}/Funds/DonationSuccess?fundId={fundId}&amount={amount}&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/Funds/Details/{fundId}",
                Metadata = new Dictionary<string, string>
                {
                    { "fundId", fundId.ToString() },
                    { "amount", amount.ToString() }
                }
            };

            var service = new Stripe.Checkout.SessionService();
            var session = await service.CreateAsync(options);

            return Json(new { success = true, sessionUrl = session.Url });
        }

        public async Task<IActionResult> DonationSuccess(int fundId, decimal amount, string session_id)
        {
            // Verify the session with Stripe
            var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(session_id);

            if (session.PaymentStatus == "paid")
            {
                var fund = await _context.Funds.FindAsync(fundId);
                if (fund != null)
                {
                    var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
                    var userEmail = HttpContext.Session.GetString("UserEmail") ?? "Anonymous";

                    // Check if donation already recorded for this session
                    var existingDonation = _context.Donations.FirstOrDefault(d => d.FundId == fundId && d.UserId == userId && d.Amount == amount && d.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
                    if (existingDonation == null)
                    {
                        var donation = new Donation
                        {
                            FundId = fundId,
                            UserId = userId,
                            DonorName = "@" + userEmail.Split('@')[0],
                            Amount = amount,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Donations.Add(donation);
                        fund.RaisedAmount += amount;
                        fund.SupportersCount += 1;

                        // Auto-mark milestones
                        var unreachedMilestones = _context.FundMilestones
                            .Where(m => m.FundId == fundId && !m.IsReached && m.TargetAmount <= fund.RaisedAmount);
                        foreach (var milestone in unreachedMilestones)
                        {
                            milestone.IsReached = true;
                            milestone.ReachedAt = DateTime.UtcNow;
                        }

                        await _context.SaveChangesAsync();
                    }
                }
            }

            return RedirectToAction("Details", new { id = fundId });
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

            // Auto-mark milestones as reached
            var unreachedMilestones = _context.FundMilestones
                .Where(m => m.FundId == fundId && !m.IsReached && m.TargetAmount <= fund.RaisedAmount)
                .ToList();

            foreach (var milestone in unreachedMilestones)
            {
                milestone.IsReached = true;
                milestone.ReachedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donation successful!" });
        }

        [HttpPost]
        public async Task<IActionResult> AddMilestone(int fundId, string title, decimal targetAmount, string? description)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can add milestones" });
            }

            var milestone = new FundMilestone
            {
                FundId = fundId,
                Title = title,
                TargetAmount = targetAmount,
                Description = description,
                IsReached = fund.RaisedAmount >= targetAmount,
                ReachedAt = fund.RaisedAmount >= targetAmount ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.FundMilestones.Add(milestone);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                milestone = new
                {
                    id = milestone.Id,
                    title = milestone.Title,
                    targetAmount = milestone.TargetAmount,
                    description = milestone.Description,
                    isReached = milestone.IsReached,
                    reachedAt = milestone.ReachedAt?.ToString("MMM d, yyyy")
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMilestone(int milestoneId)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var milestone = await _context.FundMilestones.FindAsync(milestoneId);
            if (milestone == null)
            {
                return Json(new { success = false, message = "Milestone not found" });
            }

            var fund = await _context.Funds.FindAsync(milestone.FundId);
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (fund == null || fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can delete milestones" });
            }

            _context.FundMilestones.Remove(milestone);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> PostUpdate(int fundId, string title, string content)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Only the creator or collaborators can post updates
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (!IsCreatorOrCollaborator(fund.CreatorId, userId, fundId))
            {
                return Json(new { success = false, message = "Only the fund creator or collaborators can post updates" });
            }

            var update = new FundUpdate
            {
                FundId = fundId,
                Title = title,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            _context.FundUpdates.Add(update);
            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                update = new {
                    id = update.Id,
                    title = update.Title,
                    content = update.Content,
                    createdAt = update.CreatedAt.ToString("MMM d, yyyy")
                }
            });
        }
        [HttpPost]
        public async Task<IActionResult> CreateRecurringDonation(int fundId, decimal amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in to set up a recurring donation" });
            }

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? "Anonymous";
            var donorName = "@" + userEmail.Split('@')[0];

            // Check if user already has an active recurring donation for this fund
            var existing = _context.RecurringDonations
                .FirstOrDefault(r => r.FundId == fundId && r.UserId == userId && r.IsActive);

            if (existing != null)
            {
                return Json(new { success = false, message = "You already have an active monthly donation for this fund" });
            }

            var recurring = new RecurringDonation
            {
                FundId = fundId,
                UserId = userId,
                DonorName = donorName,
                Amount = amount,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.RecurringDonations.Add(recurring);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Monthly donation of €{amount:N0} set up successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> AddCollaborator(int fundId, string email)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Only the original creator can add collaborators
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can add collaborators" });
            }

            // Find user by email
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                return Json(new { success = false, message = "No user found with that email" });
            }

            // Can't add yourself
            if (user.Id == userId)
            {
                return Json(new { success = false, message = "You are already the creator of this fund" });
            }

            // Check if already a collaborator
            var existing = _context.FundCollaborators
                .FirstOrDefault(c => c.FundId == fundId && c.UserId == user.Id);
            if (existing != null)
            {
                return Json(new { success = false, message = "This user is already a collaborator" });
            }

            var collaborator = new FundCollaborator
            {
                FundId = fundId,
                UserId = user.Id,
                Role = "collaborator",
                AddedAt = DateTime.UtcNow
            };

            _context.FundCollaborators.Add(collaborator);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                collaborator = new
                {
                    id = collaborator.Id,
                    email = user.Email,
                    role = collaborator.Role,
                    addedAt = collaborator.AddedAt.ToString("MMM d, yyyy")
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCollaborator(int collaboratorId)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var collaborator = await _context.FundCollaborators.FindAsync(collaboratorId);
            if (collaborator == null)
            {
                return Json(new { success = false, message = "Collaborator not found" });
            }

            var fund = await _context.Funds.FindAsync(collaborator.FundId);
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (fund == null || fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can remove collaborators" });
            }

            _context.FundCollaborators.Remove(collaborator);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RequestExtension(int fundId, int extensionDays, string reason)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Only the creator can request an extension
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
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
    }
}
