using Microsoft.AspNetCore.Mvc;
using KryskataFund.Data;
using KryskataFund.Models;
using Microsoft.EntityFrameworkCore;

namespace KryskataFund.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "True";
        }

        public IActionResult Dashboard()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index", "Home");
            }

            // Stats
            var allFunds = _context.Funds.ToList();
            var allDonationsList = _context.Donations.ToList();

            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalFunds = allFunds.Count;
            ViewBag.TotalDonations = allDonationsList.Count;
            ViewBag.TotalRaised = allFunds.Sum(f => f.RaisedAmount);
            ViewBag.ActiveCampaigns = allFunds.Count(f => f.EndDate > DateTime.UtcNow);
            ViewBag.CompletedCampaigns = allFunds.Count(f => f.RaisedAmount >= f.GoalAmount);

            // Recent activity
            ViewBag.RecentUsers = _context.Users.OrderByDescending(u => u.CreatedAt).Take(5).ToList();
            ViewBag.RecentFunds = _context.Funds.OrderByDescending(f => f.CreatedAt).Take(5).ToList();
            ViewBag.RecentDonations = _context.Donations.OrderByDescending(d => d.CreatedAt).Take(5).ToList();

            // All data for management
            ViewBag.AllUsers = _context.Users.OrderByDescending(u => u.CreatedAt).ToList();
            ViewBag.AllFunds = _context.Funds.OrderByDescending(f => f.CreatedAt).ToList();
            ViewBag.AllDonations = _context.Donations.OrderByDescending(d => d.CreatedAt).ToList();

            // Category breakdown
            ViewBag.CategoryStats = allFunds
                .GroupBy(f => f.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), Raised = g.Sum(f => f.RaisedAmount) })
                .ToList();

            return View();
        }

        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var user = _context.Users.Find(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            // Don't allow deleting yourself
            var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (user.Id == currentUserId)
            {
                return Json(new { success = false, message = "Cannot delete yourself" });
            }

            // Delete user's donations
            var userDonations = _context.Donations.Where(d => d.UserId == id);
            _context.Donations.RemoveRange(userDonations);

            // Delete user's funds and their donations
            var userFunds = _context.Funds.Where(f => f.CreatorId == id).ToList();
            foreach (var fund in userFunds)
            {
                var fundDonations = _context.Donations.Where(d => d.FundId == fund.Id);
                _context.Donations.RemoveRange(fundDonations);

                var fundUpdates = _context.FundUpdates.Where(u => u.FundId == fund.Id);
                _context.FundUpdates.RemoveRange(fundUpdates);
            }
            _context.Funds.RemoveRange(userFunds);

            _context.Users.Remove(user);
            _context.SaveChanges();

            return Json(new { success = true, message = "User deleted successfully" });
        }

        [HttpPost]
        public IActionResult ToggleAdmin(int id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var user = _context.Users.Find(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            // Don't allow removing your own admin
            var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (user.Id == currentUserId)
            {
                return Json(new { success = false, message = "Cannot modify your own admin status" });
            }

            user.IsAdmin = !user.IsAdmin;
            _context.SaveChanges();

            return Json(new { success = true, isAdmin = user.IsAdmin, message = user.IsAdmin ? "User is now an admin" : "Admin rights removed" });
        }

        [HttpPost]
        public IActionResult DeleteFund(int id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var fund = _context.Funds.Find(id);
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

            _context.Funds.Remove(fund);
            _context.SaveChanges();

            return Json(new { success = true, message = "Fund deleted successfully" });
        }

        [HttpPost]
        public IActionResult DeleteDonation(int id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var donation = _context.Donations.Find(id);
            if (donation == null)
            {
                return Json(new { success = false, message = "Donation not found" });
            }

            // Update the fund's raised amount
            var fund = _context.Funds.Find(donation.FundId);
            if (fund != null)
            {
                fund.RaisedAmount -= donation.Amount;
                fund.SupportersCount -= 1;
            }

            _context.Donations.Remove(donation);
            _context.SaveChanges();

            return Json(new { success = true, message = "Donation refunded and deleted" });
        }

        [HttpPost]
        public IActionResult EditFund(int id, string title, string description, decimal goalAmount)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var fund = _context.Funds.Find(id);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            fund.Title = title;
            fund.Description = description;
            fund.GoalAmount = goalAmount;
            _context.SaveChanges();

            return Json(new { success = true, message = "Fund updated successfully" });
        }

        [HttpPost]
        public IActionResult AddFundsToFund(int id, decimal amount)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var fund = _context.Funds.Find(id);
            if (fund == null)
            {
                return Json(new { success = false, message = "Fund not found" });
            }

            fund.RaisedAmount += amount;
            _context.SaveChanges();

            return Json(new { success = true, message = $"Added ${amount} to fund", newTotal = fund.RaisedAmount });
        }

        public IActionResult GetStats()
        {
            if (!IsAdmin())
            {
                return Json(new { success = false });
            }

            var today = DateTime.UtcNow.Date;
            var thisWeek = today.AddDays(-7);
            var thisMonth = today.AddDays(-30);

            var allDonations = _context.Donations.ToList();
            var allUsers = _context.Users.ToList();
            var allFunds = _context.Funds.ToList();

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
