using Microsoft.AspNetCore.Mvc;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    [Route("Funds")]
    public class FundCollaboratorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FundCollaboratorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("AddCollaborator")]
        public async Task<IActionResult> AddCollaborator(int fundId, string email)
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

            // Only the original creator can add collaborators
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can add collaborators" });
            }

            // Find user by email
            var user = _context.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                return Json(new { success = false, message = "No user found with that email" });
            }

            // Can't add yourself
            if (user.Id == userId)
            {
                return Json(new { success = false, message = "You are already the creator of this fund" });
            }

            // Check if already a collaborator
            var existing = _context.FundCollaborators
                .FirstOrDefault(c => c.FundId == fundId && c.UserId == user.Id);
            if (existing != null)
            {
                return Json(new { success = false, message = "This user is already a collaborator" });
            }

            var collaborator = new FundCollaborator
            {
                FundId = fundId,
                UserId = user.Id,
                Role = "collaborator",
                AddedAt = DateTime.UtcNow
            };

            _context.FundCollaborators.Add(collaborator);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                collaborator = new
                {
                    id = collaborator.Id,
                    email = user.Email,
                    role = collaborator.Role,
                    addedAt = collaborator.AddedAt.ToString("MMM d, yyyy")
                }
            });
        }

        [HttpPost("RemoveCollaborator")]
        public async Task<IActionResult> RemoveCollaborator(int collaboratorId)
        {
            if (HttpContext.Session.GetString("IsSignedIn") != "true")
            {
                return Json(new { success = false, message = "Please sign in" });
            }

            var collaborator = await _context.FundCollaborators.FindAsync(collaboratorId);
            if (collaborator == null)
            {
                return Json(new { success = false, message = "Collaborator not found" });
            }

            var fund = await _context.Funds.FindAsync(collaborator.FundId);
            var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
            if (fund == null || fund.CreatorId != userId)
            {
                return Json(new { success = false, message = "Only the fund creator can remove collaborators" });
            }

            _context.FundCollaborators.Remove(collaborator);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
