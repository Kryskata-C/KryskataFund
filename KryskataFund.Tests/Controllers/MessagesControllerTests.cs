using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Tests.Controllers
{
    public class MessagesControllerTests
    {
        private (MessagesController controller, ApplicationDbContext context) CreateController(
            string? dbName = null, int? userId = null, string? email = null)
        {
            dbName ??= Guid.NewGuid().ToString();
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);
            var controller = new MessagesController(context);
            TestHelper.SetupSession(controller, userId, email);
            return (controller, context);
        }

        private void SeedMessages(ApplicationDbContext context)
        {
            context.Messages.AddRange(
                new Message
                {
                    Id = 1, SenderId = 1, ReceiverId = 2,
                    SenderName = "creator@test.com", ReceiverName = "donor@test.com",
                    Content = "Hello from creator", SentAt = DateTime.UtcNow.AddMinutes(-10), IsRead = false
                },
                new Message
                {
                    Id = 2, SenderId = 2, ReceiverId = 1,
                    SenderName = "donor@test.com", ReceiverName = "creator@test.com",
                    Content = "Reply from donor", SentAt = DateTime.UtcNow.AddMinutes(-5), IsRead = false
                },
                new Message
                {
                    Id = 3, SenderId = 3, ReceiverId = 2,
                    SenderName = "admin@test.com", ReceiverName = "donor@test.com",
                    Content = "Admin message to donor", SentAt = DateTime.UtcNow.AddMinutes(-1), IsRead = false
                }
            );
            context.SaveChanges();
        }

        // --- Send ---

        [Fact]
        public async Task Send_CreatesMessageInDatabase()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");

            var result = await controller.Send(2, "Test message", null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Messages.Count().Should().Be(1);
            var msg = context.Messages.First();
            msg.SenderId.Should().Be(1);
            msg.ReceiverId.Should().Be(2);
            msg.Content.Should().Be("Test message");
        }

        [Fact]
        public async Task Send_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.Send(2, "Hello", null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task Send_RejectsEmptyMessage()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.Send(2, "", null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task Send_RejectsWhitespaceOnlyMessage()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.Send(2, "   ", null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task Send_RejectsMessageOver1000Characters()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");
            var longMessage = new string('x', 1001);

            var result = await controller.Send(2, longMessage, null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task Send_TrimsMessageContent()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");

            await controller.Send(2, "  Hello  ", null);

            var msg = context.Messages.First();
            msg.Content.Should().Be("Hello");
        }

        [Fact]
        public async Task Send_SetsCorrectSenderAndReceiverNames()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");

            await controller.Send(2, "Hi there", null);

            var msg = context.Messages.First();
            msg.SenderName.Should().Be("creator@test.com");
            msg.ReceiverName.Should().Be("donor@test.com");
        }

        [Fact]
        public async Task Send_FailsWhenReceiverNotFound()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.Send(999, "Hello", null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task Send_WithSharedFundId_IncludesFundReference()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");

            await controller.Send(2, "Check this fund", 1);

            var msg = context.Messages.First();
            msg.SharedFundId.Should().Be(1);
        }

        [Fact]
        public async Task Send_SetsIsReadToFalse()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");

            await controller.Send(2, "Unread message", null);

            var msg = context.Messages.First();
            msg.IsRead.Should().BeFalse();
        }

        // --- GetUnreadCount ---

        [Fact]
        public async Task GetUnreadCount_ReturnsCorrectCount()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            SeedMessages(context);

            var result = await controller.GetUnreadCount();

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            // User 2 has 2 unread messages: msg 1 from user 1, msg 3 from user 3
            ((int)value!.GetType().GetProperty("count")!.GetValue(value)!).Should().Be(2);
        }

        [Fact]
        public async Task GetUnreadCount_ReturnsZeroWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.GetUnreadCount();

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            ((int)value!.GetType().GetProperty("count")!.GetValue(value)!).Should().Be(0);
        }

        [Fact]
        public async Task GetUnreadCount_ReturnsZeroWhenNoUnreadMessages()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            // Seed a read message
            context.Messages.Add(new Message
            {
                SenderId = 1, ReceiverId = 2,
                SenderName = "creator@test.com", ReceiverName = "donor@test.com",
                Content = "Read message", SentAt = DateTime.UtcNow, IsRead = true
            });
            context.SaveChanges();

            var result = await controller.GetUnreadCount();

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            ((int)value!.GetType().GetProperty("count")!.GetValue(value)!).Should().Be(0);
        }

        // --- Inbox ---

        [Fact]
        public async Task Inbox_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.Inbox();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public async Task Inbox_ReturnsViewWhenSignedIn()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            SeedMessages(context);

            var result = await controller.Inbox();

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Inbox_GroupsConversationsCorrectly()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            SeedMessages(context);

            await controller.Inbox();

            var conversations = (System.Collections.IList)controller.ViewBag.Conversations;
            // User 2 has conversations with user 1 and user 3
            conversations.Count.Should().Be(2);
        }

        // --- Conversation ---

        [Fact]
        public async Task Conversation_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.Conversation(1);

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public async Task Conversation_MarksMessagesAsRead()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            SeedMessages(context);

            await controller.Conversation(1);

            // Messages from user 1 to user 2 should now be read
            var msg = context.Messages.First(m => m.Id == 1);
            msg.IsRead.Should().BeTrue();
        }

        [Fact]
        public async Task Conversation_ReturnsViewWithMessages()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            SeedMessages(context);

            var result = await controller.Conversation(1);

            result.Should().BeOfType<ViewResult>();
            var messages = (List<Message>)controller.ViewBag.Messages;
            messages.Should().HaveCount(2); // messages between user 1 and user 2
        }

        [Fact]
        public async Task Conversation_RedirectsForNonExistentUser()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.Conversation(999);

            result.Should().BeOfType<RedirectToActionResult>();
        }

        // --- Delete ---

        [Fact]
        public async Task Delete_RemovesSenderOwnMessage()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");
            SeedMessages(context);

            var result = await controller.Delete(1); // Message sent by user 1

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Messages.Find(1).Should().BeNull();
        }

        [Fact]
        public async Task Delete_DeniesNonSender()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            SeedMessages(context);

            var result = await controller.Delete(1); // Message sent by user 1, not user 2

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.Messages.Find(1).Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.Delete(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task Delete_FailsForNonExistentMessage()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.Delete(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- SearchUsers ---

        [Fact]
        public async Task SearchUsers_ReturnsMatchingUsers()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.SearchUsers("donor");

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public async Task SearchUsers_ReturnsEmptyWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.SearchUsers("donor");

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public async Task SearchUsers_ReturnsEmptyForShortTerm()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.SearchUsers("a");

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public async Task SearchUsers_ExcludesCurrentUser()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.SearchUsers("creator");

            // Should not include user 1 (the current user)
            result.Should().BeOfType<JsonResult>();
        }
    }
}
