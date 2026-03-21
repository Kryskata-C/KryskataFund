using Microsoft.AspNetCore.Mvc;
using KryskataFund.Data;
using System.Text;

namespace KryskataFund.Controllers
{
    [Route("Funds")]
    public class FundAnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FundAnalyticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsCreatorOrCollaborator(int fundCreatorId, int userId, int fundId)
        {
            if (fundCreatorId == userId) return true;
            return _context.FundCollaborators.Any(c => c.FundId == fundId && c.UserId == userId);
        }

        [HttpGet("Analytics/{id}")]
        public async Task<IActionResult> Analytics(int id)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return RedirectToAction("SignIn", "Account");
            }

            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return NotFound();
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
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

            return View("~/Views/Funds/Analytics.cshtml", fund);
        }

        [HttpGet("ExportDonors/{id}")]
        public async Task<IActionResult> ExportDonors(int id)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return RedirectToAction("SignIn", "Account");
            }

            var fund = await _context.Funds.FindAsync(id);
            if (fund == null)
            {
                return NotFound();
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
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
    }
}
