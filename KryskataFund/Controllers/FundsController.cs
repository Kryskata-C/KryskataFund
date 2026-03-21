using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KryskataFund.Models;
using KryskataFund.Data;
using KryskataFund.Constants;
using KryskataFund.Filters;
using KryskataFund.Services.Interfaces;
using Stripe;
using System.Text;

namespace KryskataFund.Controllers
{
    public class FundsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<FundsController> _logger;

        public FundsController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration configuration, IEmailService emailService, ILogger<FundsController> logger)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
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

        [RequireSignIn]
        public IActionResult Donate(int id, int amount)
        {
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
        [RequireSignIn]
        public async Task<IActionResult> CreateCheckoutSession(int fundId, decimal amount)
        {

            if (amount <= 0)
                return Json(new { success = false, message = "Amount must be greater than zero" });

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
                    { "amount", amount.ToString() },
                    { "userId", HttpContext.Session.GetString(SessionKeys.UserId) ?? "0" },
                    { "userEmail", HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "" }
                }
            };

            try
            {
                var service = new Stripe.Checkout.SessionService();
                var session = await service.CreateAsync(options);
                return Json(new { success = true, sessionUrl = session.Url });
            }
            catch (StripeException ex)
            {
                return Json(new { success = false, message = $"Stripe error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> DonationSuccess(int fundId, decimal amount, string session_id)
        {
            try
            {
                // Verify the session with Stripe
                var service = new Stripe.Checkout.SessionService();
                var session = await service.GetAsync(session_id);

                if (session.PaymentStatus == "paid")
                {
                    var fund = await _context.Funds.FindAsync(fundId);
                    if (fund != null)
                    {
                        // Try session first, fall back to Stripe metadata
                        var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
                        var userEmail = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "";

                        if (userId == 0 || string.IsNullOrEmpty(userEmail))
                        {
                            userId = int.Parse(session.Metadata.GetValueOrDefault("userId", "0"));
                            userEmail = session.Metadata.GetValueOrDefault("userEmail", "");
                        }

                        // Restore session if it was lost during Stripe redirect
                        if (HttpContext.Session.GetString(SessionKeys.IsSignedIn) != "true" && userId > 0)
                        {
                            var user = await _context.Users.FindAsync(userId);
                            if (user != null)
                            {
                                HttpContext.Session.SetString(SessionKeys.IsSignedIn, "true");
                                HttpContext.Session.SetString(SessionKeys.UserId, user.Id.ToString());
                                HttpContext.Session.SetString(SessionKeys.UserEmail, user.Email);
                                HttpContext.Session.SetString(SessionKeys.IsAdmin, user.IsAdmin.ToString());
                                userEmail = user.Email;
                            }
                        }

                        // Check if donation already recorded for this session
                        var existingDonation = _context.Donations.FirstOrDefault(d => d.FundId == fundId && d.UserId == userId && d.Amount == amount && d.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
                        if (existingDonation == null)
                        {
                            var isRelational2 = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
                            var transaction2 = isRelational2 ? await _context.Database.BeginTransactionAsync() : null;
                            try
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
                                await _context.SaveChangesAsync();

                                if (isRelational2)
                                {
                                    await _context.Database.ExecuteSqlRawAsync(
                                        "UPDATE \"Funds\" SET \"RaisedAmount\" = \"RaisedAmount\" + {0}, \"SupportersCount\" = \"SupportersCount\" + 1 WHERE \"Id\" = {1}",
                                        amount, fundId);
                                    await _context.Entry(fund).ReloadAsync();
                                }
                                else
                                {
                                    fund.RaisedAmount += amount;
                                    fund.SupportersCount += 1;
                                }

                                var unreachedMilestones = _context.FundMilestones
                                    .Where(m => m.FundId == fundId && !m.IsReached && m.TargetAmount <= fund.RaisedAmount)
                                    .ToList();
                                foreach (var milestone in unreachedMilestones)
                                {
                                    milestone.IsReached = true;
                                    milestone.ReachedAt = DateTime.UtcNow;
                                }

                                await _context.SaveChangesAsync();
                                if (transaction2 != null) await transaction2.CommitAsync();

                                // Send email to donor
                                await _emailService.SendDonationConfirmationAsync(userEmail, fund.Title, amount);

                                // Send email to fund creator
                                var creator = await _context.Users.FindAsync(fund.CreatorId);
                                if (creator != null)
                                {
                                    await _emailService.SendDonationReceivedAsync(creator.Email, "@" + userEmail.Split('@')[0], fund.Title, amount);
                                }
                            }
                            catch
                            {
                                if (transaction2 != null) await transaction2.RollbackAsync();
                            }
                        }
                    }
                }
            }
            catch (StripeException)
            {
                return RedirectToAction("Details", new { id = fundId });
            }

            return RedirectToAction("Details", new { id = fundId });
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> ProcessDonation(int fundId, decimal amount)
        {
            if (amount <= 0)
                return Json(new { success = false, message = "Amount must be greater than zero" });

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var userEmail = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "Anonymous";
            var donorName = "@" + userEmail.Split('@')[0];

            var isRelational = _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";

            var transaction = isRelational ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
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
                await _context.SaveChangesAsync();

                if (isRelational)
                {
                    // Use atomic SQL update instead of read-modify-write
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE \"Funds\" SET \"RaisedAmount\" = \"RaisedAmount\" + {0}, \"SupportersCount\" = \"SupportersCount\" + 1 WHERE \"Id\" = {1}",
                        amount, fundId);
                    await _context.Entry(fund).ReloadAsync();
                }
                else
                {
                    fund.RaisedAmount += amount;
                    fund.SupportersCount += 1;
                }

                // Auto-mark milestones
                var unreachedMilestones = _context.FundMilestones
                    .Where(m => m.FundId == fundId && !m.IsReached && m.TargetAmount <= fund.RaisedAmount)
                    .ToList();

                foreach (var milestone in unreachedMilestones)
                {
                    milestone.IsReached = true;
                    milestone.ReachedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                return Json(new { success = true, message = "Donation successful!" });
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                return Json(new { success = false, message = "An error occurred processing your donation" });
            }
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> AddMilestone(int fundId, string title, decimal targetAmount, string? description)
        {
            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
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
        [RequireSignIn]
        public async Task<IActionResult> DeleteMilestone(int milestoneId)
        {
            var milestone = await _context.FundMilestones.FindAsync(milestoneId);
            if (milestone == null)
            {
                return Json(new { success = false, message = "Milestone not found" });
            }

            var fund = await _context.Funds.FindAsync(milestone.FundId);
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (fund == null || fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can delete milestones" });
            }

            _context.FundMilestones.Remove(milestone);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> PostUpdate(int fundId, string title, string content)
        {
            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Only the creator or collaborators can post updates
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
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
        [RequireSignIn]
        public async Task<IActionResult> CreateRecurringDonation(int fundId, decimal amount)
        {
            if (amount <= 0)
                return Json(new { success = false, message = "Amount must be greater than zero" });

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var userEmail = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "Anonymous";
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
        [RequireSignIn]
        public async Task<IActionResult> AddCollaborator(int fundId, string email)
        {
            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Only the original creator can add collaborators
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
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
        [RequireSignIn]
        public async Task<IActionResult> RemoveCollaborator(int collaboratorId)
        {
            var collaborator = await _context.FundCollaborators.FindAsync(collaboratorId);
            if (collaborator == null)
            {
                return Json(new { success = false, message = "Collaborator not found" });
            }

            var fund = await _context.Funds.FindAsync(collaborator.FundId);
            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (fund == null || fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can remove collaborators" });
            }

            _context.FundCollaborators.Remove(collaborator);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> AddComment(int fundId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Comment cannot be empty" });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var userEmail = HttpContext.Session.GetString(SessionKeys.UserEmail) ?? "Anonymous";
            var userName = "@" + userEmail.Split('@')[0];

            var comment = new FundComment
            {
                FundId = fundId,
                UserId = userId,
                UserName = userName,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            _context.FundComments.Add(comment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                comment = new
                {
                    id = comment.Id,
                    userName = comment.UserName,
                    content = comment.Content,
                    createdAt = comment.CreatedAt.ToString("MMM d, yyyy h:mm tt")
                }
            });
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var comment = await _context.FundComments.FindAsync(commentId);
            if (comment == null)
            {
                return Json(new { success = false, message = "Comment not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            var fund = await _context.Funds.FindAsync(comment.FundId);

            if (comment.UserId != userId && (fund == null || fund.CreatorId != userId))
            {
                return Json(new { success = false, message = "You can only delete your own comments" });
            }

            _context.FundComments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult GetComments(int fundId)
        {
            var currentUserId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");

            var comments = _context.FundComments
                .Where(c => c.FundId == fundId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    userName = c.UserName,
                    content = c.Content,
                    userId = c.UserId,
                    createdAt = c.CreatedAt.ToString("MMM d, yyyy h:mm tt"),
                    isOwner = c.UserId == currentUserId
                })
                .ToList();

            return Json(comments);
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

        [HttpGet]
        [RequireSignIn]
        public async Task<IActionResult> ExportDonors(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return NotFound();
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (!IsCreatorOrCollaborator(fund.CreatorId, userId, id))
            {
                return Forbid();
            }

            var donations = _context.Donations
                .Where(d => d.FundId == id)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Donor Name,Amount,Date");

            foreach (var donation in donations)
            {
                sb.AppendLine($"{donation.DonorName},{donation.Amount},{donation.CreatedAt:yyyy-MM-dd}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"donors-{fund.Title.Replace(" ", "-")}.csv");
        }

        [HttpPost]
        [RequireSignIn]
        public async Task<IActionResult> RefundDonation(int donationId)
        {
            var donation = await _context.Donations.FindAsync(donationId);
            if (donation == null)
            {
                return Json(new { success = false, message = "Donation not found" });
            }

            var fund = await _context.Funds.FindAsync(donation.FundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can refund donations" });
            }

            fund.RaisedAmount -= donation.Amount;
            fund.SupportersCount = Math.Max(0, fund.SupportersCount - 1);

            _context.Donations.Remove(donation);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        [RequireSignIn]
        public async Task<IActionResult> Analytics(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return NotFound();
            }

            var userId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (!IsCreatorOrCollaborator(fund.CreatorId, userId, id))
            {
                return Forbid();
            }

            var donations = _context.Donations
                .Where(d => d.FundId == id)
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            var dailyTotals = donations
                .GroupBy(d => d.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key.ToString("MMM d"), Amount = g.Sum(d => d.Amount) })
                .ToList();

            var topDonors = donations
                .GroupBy(d => d.DonorName)
                .Select(g => new { DonorName = g.Key, TotalAmount = g.Sum(d => d.Amount), DonationCount = g.Count() })
                .OrderByDescending(d => d.TotalAmount)
                .Take(10)
                .ToList();

            var uniqueDonors = donations.Select(d => d.UserId).Distinct().Count();

            ViewBag.Fund = fund;
            ViewBag.Donations = donations;
            ViewBag.DailyTotals = dailyTotals;
            ViewBag.TopDonors = topDonors;
            ViewBag.UniqueDonors = uniqueDonors;

            return View(fund);
        }
    }
}
