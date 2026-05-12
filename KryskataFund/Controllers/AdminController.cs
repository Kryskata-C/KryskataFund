using Microsoft.AspNetCore.Mvc;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Constants;
using KryskataFund.Filters;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace KryskataFund.Controllers
{
    [RequireAdmin]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var allFunds = await _context.Funds.ToListAsync();
            var allDonationsList = await _context.Donations.ToListAsync();

            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalFunds = allFunds.Count;
            ViewBag.TotalDonations = allDonationsList.Count;
            ViewBag.TotalRaised = allFunds.Sum(f => f.RaisedAmount);
            ViewBag.ActiveCampaigns = allFunds.Count(f => f.EndDate > DateTime.UtcNow);
            ViewBag.CompletedCampaigns = allFunds.Count(f => f.RaisedAmount >= f.GoalAmount);

            ViewBag.RecentUsers = await _context.Users.OrderByDescending(u => u.CreatedAt).Take(5).ToListAsync();
            ViewBag.RecentFunds = await _context.Funds.OrderByDescending(f => f.CreatedAt).Take(5).ToListAsync();
            ViewBag.RecentDonations = await _context.Donations.OrderByDescending(d => d.CreatedAt).Take(5).ToListAsync();

            ViewBag.AllUsers = await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
            ViewBag.AllFunds = await _context.Funds.OrderByDescending(f => f.CreatedAt).ToListAsync();
            ViewBag.AllDonations = await _context.Donations.OrderByDescending(d => d.CreatedAt).ToListAsync();

            ViewBag.CategoryStats = allFunds
                .GroupBy(f => f.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), Raised = g.Sum(f => f.RaisedAmount) })
                .ToList();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            // Don't allow deleting yourself
            var currentUserId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (user.Id == currentUserId)
            {
                return Json(new { success = false, message = "Cannot delete yourself" });
            }

            // Delete user's donations
            var userDonations = _context.Donations.Where(d => d.UserId == id);
            _context.Donations.RemoveRange(userDonations);

            // Delete user's collaborator entries
            var userCollabs = _context.FundCollaborators.Where(c => c.UserId == id);
            _context.FundCollaborators.RemoveRange(userCollabs);

            // Delete user's recurring donations
            var userRecurring = _context.RecurringDonations.Where(r => r.UserId == id);
            _context.RecurringDonations.RemoveRange(userRecurring);

            // Delete user's follows
            var userFollows = _context.UserFollows.Where(f => f.UserId == id);
            _context.UserFollows.RemoveRange(userFollows);

            // Delete user's funds and their donations
            var userFunds = await _context.Funds.Where(f => f.CreatorId == id).ToListAsync();
            foreach (var fund in userFunds)
            {
                var fundDonations = _context.Donations.Where(d => d.FundId == fund.Id);
                _context.Donations.RemoveRange(fundDonations);

                var fundUpdates = _context.FundUpdates.Where(u => u.FundId == fund.Id);
                _context.FundUpdates.RemoveRange(fundUpdates);

                // Delete fund's milestones
                var fundMilestones = _context.FundMilestones.Where(m => m.FundId == fund.Id);
                _context.FundMilestones.RemoveRange(fundMilestones);

                // Delete fund's collaborators
                var fundCollaborators = _context.FundCollaborators.Where(c => c.FundId == fund.Id);
                _context.FundCollaborators.RemoveRange(fundCollaborators);

                // Delete fund's deadline extensions
                var fundExtensions = _context.DeadlineExtensions.Where(e => e.FundId == fund.Id);
                _context.DeadlineExtensions.RemoveRange(fundExtensions);

                // Delete fund's recurring donations
                var fundRecurring = _context.RecurringDonations.Where(r => r.FundId == fund.Id);
                _context.RecurringDonations.RemoveRange(fundRecurring);

                // Delete fund's follows
                var fundFollows = _context.UserFollows.Where(f => f.FundId == fund.Id);
                _context.UserFollows.RemoveRange(fundFollows);
            }
            _context.Funds.RemoveRange(userFunds);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "User deleted successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAdmin(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            // Don't allow removing your own admin
            var currentUserId = int.Parse(HttpContext.Session.GetString(SessionKeys.UserId) ?? "0");
            if (user.Id == currentUserId)
            {
                return Json(new { success = false, message = "Cannot modify your own admin status" });
            }

            user.IsAdmin = !user.IsAdmin;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isAdmin = user.IsAdmin, message = user.IsAdmin ? "User is now an admin" : "Admin rights removed" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFund(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Delete fund's donations
            var fundDonations = _context.Donations.Where(d => d.FundId == id);
            _context.Donations.RemoveRange(fundDonations);

            // Delete fund's updates
            var fundUpdates = _context.FundUpdates.Where(u => u.FundId == id);
            _context.FundUpdates.RemoveRange(fundUpdates);

            // Delete fund's milestones
            var fundMilestones = _context.FundMilestones.Where(m => m.FundId == id);
            _context.FundMilestones.RemoveRange(fundMilestones);

            // Delete fund's collaborators
            var fundCollaborators = _context.FundCollaborators.Where(c => c.FundId == id);
            _context.FundCollaborators.RemoveRange(fundCollaborators);

            // Delete fund's deadline extensions
            var fundExtensions = _context.DeadlineExtensions.Where(e => e.FundId == id);
            _context.DeadlineExtensions.RemoveRange(fundExtensions);

            // Delete fund's recurring donations
            var fundRecurring = _context.RecurringDonations.Where(r => r.FundId == id);
            _context.RecurringDonations.RemoveRange(fundRecurring);

            // Delete fund's follows
            var fundFollows = _context.UserFollows.Where(f => f.FundId == id);
            _context.UserFollows.RemoveRange(fundFollows);

            _context.Funds.Remove(fund);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Fund deleted successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDonation(int id)
        {
            var donation = await _context.Donations.FindAsync(id);
            if (donation == null)
            {
                return Json(new { success = false, message = "Donation not found" });
            }

            var fund = await _context.Funds.FindAsync(donation.FundId);
            if (fund != null)
            {
                fund.RaisedAmount -= donation.Amount;
                fund.SupportersCount -= 1;
            }

            _context.Donations.Remove(donation);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donation refunded and deleted" });
        }

        [HttpPost]
        public async Task<IActionResult> EditFund(int id, string title, string description, decimal goalAmount)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            // Input validation
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            {
                return Json(new { success = false, message = "Title is required and cannot exceed 200 characters" });
            }

            if (goalAmount <= 0)
            {
                return Json(new { success = false, message = "Goal amount must be greater than 0" });
            }

            fund.Title = title.Trim();
            fund.Description = new HtmlSanitizer().Sanitize(description);
            fund.GoalAmount = goalAmount;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Fund updated successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> AddFundsToFund(int id, decimal amount)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            if (amount <= 0)
            {
                return Json(new { success = false, message = "Amount must be greater than 0" });
            }

            fund.RaisedAmount += amount;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Added ${amount} to fund", newTotal = fund.RaisedAmount });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleVerified(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            fund.IsVerified = !fund.IsVerified;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isVerified = fund.IsVerified, message = fund.IsVerified ? "Fund verified" : "Verification removed" });
        }

        public IActionResult Criteria()
        {
            return View();
        }

        public async Task<IActionResult> GetStats()
        {
            var today = DateTime.UtcNow.Date;
            var thisWeek = today.AddDays(-7);
            var thisMonth = today.AddDays(-30);

            var allDonations = await _context.Donations.ToListAsync();
            var allUsers = await _context.Users.ToListAsync();
            var allFunds = await _context.Funds.ToListAsync();

            return Json(new
            {
                success = true,
                todayDonations = allDonations.Count(d => d.CreatedAt.Date == today),
                todayAmount = allDonations.Where(d => d.CreatedAt.Date == today).Sum(d => d.Amount),
                weekDonations = allDonations.Count(d => d.CreatedAt >= thisWeek),
                weekAmount = allDonations.Where(d => d.CreatedAt >= thisWeek).Sum(d => d.Amount),
                monthDonations = allDonations.Count(d => d.CreatedAt >= thisMonth),
                monthAmount = allDonations.Where(d => d.CreatedAt >= thisMonth).Sum(d => d.Amount),
                newUsersToday = allUsers.Count(u => u.CreatedAt.Date == today),
                newUsersWeek = allUsers.Count(u => u.CreatedAt >= thisWeek),
                newFundsToday = allFunds.Count(f => f.CreatedAt.Date == today),
                newFundsWeek = allFunds.Count(f => f.CreatedAt >= thisWeek)
            });
        }
    }
}
