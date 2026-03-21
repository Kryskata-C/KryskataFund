using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    [Route("Funds")]
    public class FundCommentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FundCommentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("AddComment")]
        public async Task<IActionResult> AddComment(int fundId, string content)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Comment cannot be empty" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var userEmail = HttpContext.Session.GetString("UserEmail") ?? "Anonymous";
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

        [HttpPost("DeleteComment")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var comment = await _context.FundComments.FindAsync(commentId);
            if (comment == null)
            {
                return Json(new { success = false, message = "Comment not found" });
            }

            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            var fund = await _context.Funds.FindAsync(comment.FundId);

            if (comment.UserId != userId && (fund == null || fund.CreatorId != userId))
            {
                return Json(new { success = false, message = "You can only delete your own comments" });
            }

            _context.FundComments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet("GetComments")]
        public IActionResult GetComments(int fundId)
        {
            var currentUserId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

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
    }
}
