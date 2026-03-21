using Microsoft.AspNetCore.Mvc;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Search?q=term
        public IActionResult Index(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                ViewBag.Query = "";
                ViewBag.Results = new List<KryskataFund.Models.Fund>();
                return View();
            }

            var query = q.Trim().ToLower();
            ViewBag.Query = System.Net.WebUtility.HtmlEncode(q);

            var results = _context.Funds
                .AsEnumerable()
                .Where(f => f.Title.ToLower().Contains(query)
                    || f.Description.ToLower().Contains(query)
                    || f.Category.ToLower().Contains(query)
                    || f.CreatorName.ToLower().Contains(query))
                .OrderByDescending(f => f.RaisedAmount)
                .ToList();

            ViewBag.Results = results;
            return View();
        }

        // GET: /Search/Autocomplete?term=abc
        public IActionResult Autocomplete(string? term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                return Json(new List<object>());

            var query = term.Trim().ToLower();

            var results = _context.Funds
                .AsEnumerable()
                .Where(f => f.Title.ToLower().Contains(query) || f.Category.ToLower().Contains(query))
                .Take(5)
                .Select(f => new {
                    id = f.Id,
                    title = f.Title,
                    category = f.Category,
                    categoryColor = f.CategoryColor,
                    raised = f.RaisedAmount,
                    goal = f.GoalAmount,
                    progress = f.ProgressPercent,
                    imageUrl = f.ImageUrl
                })
                .ToList();

            return Json(results);
        }
    }
}
