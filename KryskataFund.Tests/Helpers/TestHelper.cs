using KryskataFund.Data;
using KryskataFund.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace KryskataFund.Tests.Helpers
{
    public static class TestHelper
    {
        public static ApplicationDbContext CreateDbContext(string? dbName = null)
        {
            dbName ??= Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options);
        }

        public static void SetupSession(Controller controller, int? userId = null, string? email = null, bool isAdmin = false)
        {
            var httpContext = new DefaultHttpContext();
            var session = new MockSession();

            if (userId.HasValue)
            {
                session.SetString("IsSignedIn", "true");
                session.SetString("UserId", userId.Value.ToString());
                session.SetString("UserEmail", email ?? "test@test.com");
                session.SetString("IsAdmin", isAdmin ? "True" : "False");
            }

            httpContext.Session = session;

            // Setup IUrlHelper to support Url.IsLocalUrl
            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>())).Returns(false);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.Url = mockUrlHelper.Object;
        }

        public static void SeedTestData(ApplicationDbContext context)
        {
            context.Users.AddRange(
                new User { Id = 1, Email = "creator@test.com", PasswordHash = HashPassword("Password1"), IsAdmin = false },
                new User { Id = 2, Email = "donor@test.com", PasswordHash = HashPassword("Password1"), IsAdmin = false },
                new User { Id = 3, Email = "admin@test.com", PasswordHash = HashPassword("Password1"), IsAdmin = true }
            );

            context.Funds.AddRange(
                new Fund { Id = 1, Title = "Test Fund 1", Description = "Test description 1", Category = "Education", GoalAmount = 1000, RaisedAmount = 500, SupportersCount = 5, CreatorId = 1, CreatorName = "@creator", EndDate = DateTime.UtcNow.AddDays(30), CategoryColor = "#4ade80" },
                new Fund { Id = 2, Title = "Test Fund 2", Description = "Test description 2", Category = "Health", GoalAmount = 5000, RaisedAmount = 0, SupportersCount = 0, CreatorId = 1, CreatorName = "@creator", EndDate = DateTime.UtcNow.AddDays(60), CategoryColor = "#f97316" },
                new Fund { Id = 3, Title = "Ended Fund", Description = "This fund has ended", Category = "Animals", GoalAmount = 2000, RaisedAmount = 1500, SupportersCount = 10, CreatorId = 2, CreatorName = "@donor", EndDate = DateTime.UtcNow.AddDays(-1), CategoryColor = "#22d3ee" }
            );

            context.Donations.AddRange(
                new Donation { Id = 1, FundId = 1, UserId = 2, DonorName = "@donor", Amount = 100, CreatedAt = DateTime.UtcNow },
                new Donation { Id = 2, FundId = 1, UserId = 2, DonorName = "@donor", Amount = 50, CreatedAt = DateTime.UtcNow.AddDays(-1) }
            );

            context.SaveChanges();
        }

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }

    public class MockSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public string Id => Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
