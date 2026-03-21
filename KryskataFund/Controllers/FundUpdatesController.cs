using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    [Route("Funds")]
    public class FundUpdatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FundUpdatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsCreatorOrCollaborator(int fundCreatorId, int userId, int fundId)
        {
            if (fundCreatorId == userId) return true;
            return _context.FundCollaborators.Any(c => c.FundId == fundId && c.UserId == userId);
        }

        [HttpPost("PostUpdate")]
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

            // Input validation
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            {
                return Json(new { success = false, message = "Title is required and cannot exceed 200 characters" });
            }

            if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 5000)
            {
                return Json(new { success = false, message = "Content is required and cannot exceed 5000 characters" });
            }

            title = title.Trim();
            content = content.Trim();

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
    }
}
