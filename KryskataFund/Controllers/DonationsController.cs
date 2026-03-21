using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KryskataFund.Models;
using KryskataFund.Data;
using KryskataFund.Services.Interfaces;
using Stripe;

namespace KryskataFund.Controllers
{
    [Route("Funds")]
    public class DonationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public DonationsController(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        [HttpGet("Donate")]
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
            return View("~/Views/Funds/Donate.cshtml", fund);
        }

        [HttpPost("CreateCheckoutSession")]
        public async Task<IActionResult> CreateCheckoutSession(int fundId, decimal amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
                return Json(new { success = false, message = "Please sign in" });

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
                    { "userId", HttpContext.Session.GetString("UserId") ?? "0" },
                    { "userEmail", HttpContext.Session.GetString("UserEmail") ?? "" }
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

        [HttpGet("DonationSuccess")]
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
                        var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
                        var userEmail = HttpContext.Session.GetString("UserEmail") ?? "";

                        if (userId == 0 || string.IsNullOrEmpty(userEmail))
                        {
                            userId = int.Parse(session.Metadata.GetValueOrDefault("userId", "0"));
                            userEmail = session.Metadata.GetValueOrDefault("userEmail", "");
                        }

                        // Restore session if it was lost during Stripe redirect
                        if (HttpContext.Session.GetString("IsSignedIn") != "true" && userId > 0)
                        {
                            var user = await _context.Users.FindAsync(userId);
                            if (user != null)
                            {
                                HttpContext.Session.SetString("IsSignedIn", "true");
                                HttpContext.Session.SetString("UserId", user.Id.ToString());
                                HttpContext.Session.SetString("UserEmail", user.Email);
                                HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());
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
                return RedirectToAction("Details", "Funds", new { id = fundId });
            }

            return RedirectToAction("Details", "Funds", new { id = fundId });
        }

        [HttpPost("ProcessDonation")]
        public async Task<IActionResult> ProcessDonation(int fundId, decimal amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in to donate" });
            }

            if (amount <= 0)
                return Json(new { success = false, message = "Amount must be greater than zero" });

            var fund = await _context.Funds.FindAsync(fundId);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? "Anonymous";
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

        [HttpPost("CreateRecurringDonation")]
        public async Task<IActionResult> CreateRecurringDonation(int fundId, decimal amount)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in to set up a recurring donation" });
            }

            if (amount <= 0)
                return Json(new { success = false, message = "Amount must be greater than zero" });

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

        [HttpPost("RefundDonation")]
        public async Task<IActionResult> RefundDonation(int donationId)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

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

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
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
    }
}
