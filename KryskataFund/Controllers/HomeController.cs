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
            var allDonations = _context.Donations.ToList();

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

            // Calculate today's impact (donations made today)
            var today = DateTime.UtcNow.Date;
            var todaysDonations = allDonations.Where(d => d.CreatedAt.Date == today).ToList();
            ViewBag.TodaysImpact = todaysDonations.Sum(d => d.Amount);

            // Calculate average support per donation
            ViewBag.AvgSupport = allDonations.Count > 0
                ? Math.Round(allDonations.Average(d => d.Amount), 0)
                : 0;

            // Get top category
            var topCategory = categoryCounts.OrderByDescending(c => c.Value).FirstOrDefault();
            ViewBag.TopCategory = topCategory.Key ?? "None";

            // Filter funds if category is selected
            var funds = string.IsNullOrEmpty(category)
                ? allFunds.OrderByDescending(f => f.CreatedAt).ToList()
                : allFunds.Where(f => f.Category == category).OrderByDescending(f => f.CreatedAt).ToList();

            // Get followed fund IDs for current user
            var followedFundIds = new List<int>();
            if (HttpContext.Session.GetString("IsSignedIn") == "true")
            {
                var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
                followedFundIds = _context.UserFollows
                    .Where(f => f.UserId == userId)
                    .Select(f => f.FundId)
                    .ToList();
            }
            ViewBag.FollowedFundIds = followedFundIds;

            return View(funds);
        }

        public IActionResult GetRecentActivity()
        {
            var activities = new List<object>();

            // Get recent donations (last 10)
            var recentDonations = _context.Donations
                .OrderByDescending(d => d.CreatedAt)
                .Take(10)
                .ToList();

            foreach (var donation in recentDonations)
            {
                var fund = _context.Funds.FirstOrDefault(f => f.Id == donation.FundId);
                activities.Add(new
                {
                    type = "donation",
                    donorName = donation.DonorName,
                    amount = donation.Amount,
                    fundTitle = fund?.Title ?? "Unknown fund",
                    timeAgo = GetTimeAgo(donation.CreatedAt),
                    timestamp = donation.CreatedAt
                });
            }

            // Get recent funds (last 5)
            var recentFunds = _context.Funds
                .OrderByDescending(f => f.CreatedAt)
                .Take(5)
                .ToList();

            foreach (var fund in recentFunds)
            {
                activities.Add(new
                {
                    type = "new_fund",
                    creatorName = fund.CreatorName,
                    fundTitle = fund.Title,
                    timeAgo = GetTimeAgo(fund.CreatedAt),
                    timestamp = fund.CreatedAt
                });

                // Check for milestones (100% funded)
                if (fund.ProgressPercent >= 100)
                {
                    activities.Add(new
                    {
                        type = "milestone",
                        fundTitle = fund.Title,
                        percent = fund.ProgressPercent,
                        timeAgo = GetTimeAgo(fund.CreatedAt),
                        timestamp = fund.CreatedAt
                    });
                }
            }

            // Sort by timestamp and take most recent
            var sortedActivities = activities
                .OrderByDescending(a => ((dynamic)a).timestamp)
                .Take(5)
                .ToList();

            return Json(sortedActivities);
        }

        public IActionResult GetLiveStats()
        {
            var allDonations = _context.Donations.ToList();
            var today = DateTime.UtcNow.Date;
            var todaysDonations = allDonations.Where(d => d.CreatedAt.Date == today).ToList();

            return Json(new
            {
                todaysImpact = todaysDonations.Sum(d => d.Amount),
                avgSupport = allDonations.Count > 0 ? Math.Round(allDonations.Average(d => d.Amount), 0) : 0
            });
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;
            if (timeSpan.TotalSeconds < 60) return "just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d ago";
            return dateTime.ToString("MMM d");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Leaderboard()
        {
            var allFunds = _context.Funds.ToList();
            var allDonations = _context.Donations.ToList();
            var allUsers = _context.Users.ToList();

            // Top funded campaigns
            var topCampaigns = allFunds
                .OrderByDescending(f => f.RaisedAmount)
                .Take(10)
                .ToList();

            // Top donors (by total amount donated)
            var topDonors = allDonations
                .GroupBy(d => d.UserId)
                .Select(g => new {
                    UserId = g.Key,
                    DonorName = g.First().DonorName,
                    TotalDonated = g.Sum(d => d.Amount),
                    DonationCount = g.Count()
                })
                .OrderByDescending(d => d.TotalDonated)
                .Take(10)
                .ToList();

            // Most active creators (by funds raised)
            var topCreators = allFunds
                .GroupBy(f => f.CreatorId)
                .Select(g => new {
                    CreatorId = g.Key,
                    CreatorName = g.First().CreatorName,
                    TotalRaised = g.Sum(f => f.RaisedAmount),
                    CampaignCount = g.Count(),
                    TotalSupporters = g.Sum(f => f.SupportersCount)
                })
                .OrderByDescending(c => c.TotalRaised)
                .Take(10)
                .ToList();

            // Stats
            ViewBag.TotalRaised = allFunds.Sum(f => f.RaisedAmount);
            ViewBag.TotalDonations = allDonations.Count;
            ViewBag.TotalCampaigns = allFunds.Count;
            ViewBag.TotalUsers = allUsers.Count;

            ViewBag.TopCampaigns = topCampaigns;
            ViewBag.TopDonors = topDonors;
            ViewBag.TopCreators = topCreators;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
