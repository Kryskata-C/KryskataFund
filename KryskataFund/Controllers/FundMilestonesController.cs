using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    [Route("Funds")]
    public class FundMilestonesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FundMilestonesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("AddMilestone")]
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

            // Input validation
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            {
                return Json(new { success = false, message = "Title is required and cannot exceed 200 characters" });
            }

            if (targetAmount <= 0)
            {
                return Json(new { success = false, message = "Target amount must be greater than 0" });
            }

            if (description != null && description.Length > 2000)
            {
                return Json(new { success = false, message = "Description cannot exceed 2000 characters" });
            }

            title = title.Trim();
            description = description?.Trim();

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

        [HttpPost("DeleteMilestone")]
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
    }
}
