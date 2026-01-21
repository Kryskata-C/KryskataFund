using KryskataFund.Models;
using KryskataFund.Data;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace KryskataFund.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index(string? category = null)
        {
            var allFunds = _context.Funds.ToList();

            // Get category counts
            var categoryCounts = allFunds
                .GroupBy(f => f.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.CategoryCounts = categoryCounts;
            ViewBag.TotalCount = allFunds.Count;
            ViewBag.SelectedCategory = category;

            // Calculate total raised and live campaigns
            ViewBag.TotalRaised = allFunds.Sum(f => f.RaisedAmount);
            ViewBag.LiveCampaigns = allFunds.Count(f => f.DaysLeft > 0);

            // Filter funds if category is selected
            var funds = string.IsNullOrEmpty(category)
                ? allFunds.OrderByDescending(f => f.CreatedAt).ToList()
                : allFunds.Where(f => f.Category == category).OrderByDescending(f => f.CreatedAt).ToList();

            return View(funds);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
