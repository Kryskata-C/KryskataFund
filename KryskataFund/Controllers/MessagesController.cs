using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KryskataFund.Models;
using KryskataFund.Data;

namespace KryskataFund.Controllers
{
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (int.TryParse(userIdStr, out var userId))
                return userId;
            return null;
        }

        private bool IsSignedIn()
        {
            return HttpContext.Session.GetString("IsSignedIn") == "true";
        }

        public async Task<IActionResult> Inbox()
        {
            if (!IsSignedIn())
                return RedirectToAction("SignIn", "Account", new { returnUrl = "/Messages/Inbox" });

            var userId = GetCurrentUserId()!.Value;

            // Get all messages involving this user
            var messages = await _context.Messages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            // Group by the other user
            var conversations = messages
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var lastMsg = g.First();
                    var otherUserId = g.Key;
                    var otherUserName = lastMsg.SenderId == userId ? lastMsg.ReceiverName : lastMsg.SenderName;
                    var unreadCount = g.Count(m => m.ReceiverId == userId && !m.IsRead);
                    var preview = lastMsg.Content.Length > 50 ? lastMsg.Content.Substring(0, 50) + "..." : lastMsg.Content;

                    return new
                    {
                        OtherUserId = otherUserId,
                        OtherUserName = otherUserName,
                        LastMessage = preview,
                        SentAt = lastMsg.SentAt,
                        UnreadCount = unreadCount,
                        IsSentByMe = lastMsg.SenderId == userId
                    };
                })
                .OrderByDescending(c => c.SentAt)
                .ToList();

            ViewBag.Conversations = conversations;
            ViewBag.CurrentUserId = userId;

            return View();
        }

        public async Task<IActionResult> Conversation(int userId)
        {
            if (!IsSignedIn())
                return RedirectToAction("SignIn", "Account", new { returnUrl = $"/Messages/Conversation?userId={userId}" });

            var currentUserId = GetCurrentUserId()!.Value;

            // Mark all messages from this user as read
            var unreadMessages = await _context.Messages
                .Where(m => m.SenderId == userId && m.ReceiverId == currentUserId && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();

            // Load conversation
            var messages = await _context.Messages
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                    (m.SenderId == userId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var otherUser = await _context.Users.FindAsync(userId);
            if (otherUser == null)
                return RedirectToAction("Inbox");

            ViewBag.Messages = messages;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.OtherUserId = userId;
            ViewBag.OtherUserEmail = otherUser.Email;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Send(int receiverId, string content)
        {
            if (!IsSignedIn())
                return Json(new { success = false, error = "Not signed in" });

            var currentUserId = GetCurrentUserId()!.Value;

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, error = "Message cannot be empty" });

            if (content.Length > 1000)
                return Json(new { success = false, error = "Message cannot exceed 1000 characters" });

            var sender = await _context.Users.FindAsync(currentUserId);
            var receiver = await _context.Users.FindAsync(receiverId);

            if (sender == null || receiver == null)
                return Json(new { success = false, error = "User not found" });

            var message = new Message
            {
                SenderId = currentUserId,
                ReceiverId = receiverId,
                SenderName = sender.Email,
                ReceiverName = receiver.Email,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = new
                {
                    message.Id,
                    message.SenderId,
                    message.ReceiverId,
                    message.SenderName,
                    message.ReceiverName,
                    message.Content,
                    SentAt = message.SentAt.ToString("MMM d, h:mm tt")
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string term)
        {
            if (!IsSignedIn())
                return Json(new List<object>());

            var currentUserId = GetCurrentUserId()!.Value;

            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                return Json(new List<object>());

            var query = term.Trim().ToLower();
            var users = await _context.Users
                .Where(u => u.Id != currentUserId && u.Email.ToLower().Contains(query))
                .Take(5)
                .Select(u => new { u.Id, u.Email })
                .ToListAsync();

            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (!IsSignedIn())
                return Json(new { count = 0 });

            var userId = GetCurrentUserId()!.Value;
            var count = await _context.Messages
                .CountAsync(m => m.ReceiverId == userId && !m.IsRead);

            return Json(new { count });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsSignedIn())
                return Json(new { success = false, error = "Not signed in" });

            var userId = GetCurrentUserId()!.Value;
            var message = await _context.Messages.FindAsync(id);

            if (message == null)
                return Json(new { success = false, error = "Message not found" });

            if (message.SenderId != userId)
                return Json(new { success = false, error = "You can only delete your own messages" });

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
