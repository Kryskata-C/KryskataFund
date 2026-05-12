using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Data;
using KryskataFund.Filters;
using KryskataFund.Models;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace KryskataFund.Tests.Controllers
{
    public class AdminControllerTests
    {
        private (AdminController controller, ApplicationDbContext context) CreateController(string? dbName = null, int? userId = null, string? email = null, bool isAdmin = false)
        {
            dbName ??= Guid.NewGuid().ToString();
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);
            var controller = new AdminController(context);
            TestHelper.SetupSession(controller, userId, email, isAdmin);
            return (controller, context);
        }

        // --- RequireAdmin attribute is applied at class level ---

        [Fact]
        public void AdminController_HasRequireAdminAttribute()
        {
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty("AdminController should have [RequireAdmin] attribute");
        }

        [Fact]
        public void RequireAdminFilter_RedirectsNonAdminUsers()
        {
            var filter = new RequireAdminAttribute();
            var httpContext = new DefaultHttpContext();
            var session = new MockSession();
            session.SetString("IsAdmin", "False");
            session.SetString("IsSignedIn", "true");
            httpContext.Session = session;

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var context = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object());

            filter.OnActionExecuting(context);

            context.Result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)context.Result;
            redirect.ActionName.Should().Be("Forbidden");
            redirect.ControllerName.Should().Be("Error");
        }

        [Fact]
        public void RequireAdminFilter_AllowsAdminUsers()
        {
            var filter = new RequireAdminAttribute();
            var httpContext = new DefaultHttpContext();
            var session = new MockSession();
            session.SetString("IsAdmin", "True");
            session.SetString("IsSignedIn", "true");
            httpContext.Session = session;

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var context = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object());

            filter.OnActionExecuting(context);

            context.Result.Should().BeNull("admin users should not be redirected");
        }

        [Fact]
        public void RequireAdminFilter_RedirectsWhenNoSessionSet()
        {
            var filter = new RequireAdminAttribute();
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new MockSession();

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var context = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object());

            filter.OnActionExecuting(context);

            context.Result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)context.Result;
            redirect.ActionName.Should().Be("Forbidden");
            redirect.ControllerName.Should().Be("Error");
        }

        // --- Dashboard ---

        [Fact]
        public void Dashboard_RedirectsNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            // Verify the attribute is present (filter bypass in unit tests is expected).
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Dashboard_ReturnsViewForAdmin()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.Dashboard();

            result.Should().BeOfType<ViewResult>();
            ((int)controller.ViewBag.TotalUsers).Should().Be(3);
            ((int)controller.ViewBag.TotalFunds).Should().Be(3);
            ((int)controller.ViewBag.TotalDonations).Should().Be(2);
        }

        [Fact]
        public void Dashboard_RedirectsWhenNotSignedIn()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        // --- DeleteUser ---

        [Fact]
        public async Task DeleteUser_RemovesUser()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteUser(2);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Users.Find(2).Should().BeNull();
        }

        [Fact]
        public async Task DeleteUser_RemovesUserDonationsAndFunds()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            await controller.DeleteUser(1); // Creator has funds 1 and 2

            context.Funds.Any(f => f.CreatorId == 1).Should().BeFalse();
        }

        [Fact]
        public void DeleteUser_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        [Fact]
        public async Task DeleteUser_PreventsDeleteSelf()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteUser(3);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task DeleteUser_HandlesNonExistentUser()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteUser(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- ToggleAdmin ---

        [Fact]
        public async Task ToggleAdmin_TogglesFlag()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.ToggleAdmin(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            var user = context.Users.Find(1)!;
            user.IsAdmin.Should().BeTrue();
        }

        [Fact]
        public async Task ToggleAdmin_TogglesBack()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            await controller.ToggleAdmin(1); // Make admin
            await controller.ToggleAdmin(1); // Remove admin

            var user = context.Users.Find(1)!;
            user.IsAdmin.Should().BeFalse();
        }

        [Fact]
        public async Task ToggleAdmin_PreventsToggleSelf()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.ToggleAdmin(3);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void ToggleAdmin_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        // --- DeleteFund ---

        [Fact]
        public async Task DeleteFund_RemovesFundAndDonations()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteFund(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Funds.Find(1).Should().BeNull();
            context.Donations.Any(d => d.FundId == 1).Should().BeFalse();
        }

        [Fact]
        public void DeleteFund_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        [Fact]
        public async Task DeleteFund_HandlesNonExistentFund()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteFund(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- DeleteDonation ---

        [Fact]
        public async Task DeleteDonation_RemovesAndRefunds()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteDonation(1); // Donation of 100 to Fund 1

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Donations.Find(1).Should().BeNull();
            var fund = context.Funds.Find(1)!;
            fund.RaisedAmount.Should().Be(400); // 500 - 100
            fund.SupportersCount.Should().Be(4); // 5 - 1
        }

        [Fact]
        public void DeleteDonation_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        [Fact]
        public async Task DeleteDonation_HandlesNonExistentDonation()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.DeleteDonation(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- EditFund ---

        [Fact]
        public async Task EditFund_UpdatesFundDetails()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.EditFund(1, "Updated Title", "Updated Description", 2000);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            var fund = context.Funds.Find(1)!;
            fund.Title.Should().Be("Updated Title");
            fund.Description.Should().Be("Updated Description");
            fund.GoalAmount.Should().Be(2000);
        }

        [Fact]
        public void EditFund_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        // --- AddFundsToFund ---

        [Fact]
        public async Task AddFundsToFund_IncreasesRaisedAmount()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.AddFundsToFund(1, 300);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Funds.Find(1)!.RaisedAmount.Should().Be(800); // 500 + 300
        }

        [Fact]
        public void AddFundsToFund_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        // --- ToggleVerified ---

        [Fact]
        public async Task ToggleVerified_TogglesFlag()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.ToggleVerified(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Funds.Find(1)!.IsVerified.Should().BeTrue();
        }

        [Fact]
        public async Task ToggleVerified_TogglesBack()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            await controller.ToggleVerified(1);
            await controller.ToggleVerified(1);

            context.Funds.Find(1)!.IsVerified.Should().BeFalse();
        }

        [Fact]
        public void ToggleVerified_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }

        // --- GetStats ---

        [Fact]
        public async Task GetStats_ReturnsDataForAdmin()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = await controller.GetStats();

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        }

        [Fact]
        public void GetStats_DeniesNonAdmin()
        {
            // Auth is handled by [RequireAdmin] filter at class level.
            var attr = typeof(AdminController).GetCustomAttributes(typeof(RequireAdminAttribute), true);
            attr.Should().NotBeEmpty();
        }
    }
}
