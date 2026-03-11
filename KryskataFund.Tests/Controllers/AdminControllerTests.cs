using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

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

        // --- Dashboard ---

        [Fact]
        public void Dashboard_RedirectsNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.Dashboard();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void Dashboard_ReturnsViewForAdmin()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.Dashboard();

            result.Should().BeOfType<ViewResult>();
            ((int)controller.ViewBag.TotalUsers).Should().Be(3);
            ((int)controller.ViewBag.TotalFunds).Should().Be(3);
            ((int)controller.ViewBag.TotalDonations).Should().Be(2);
        }

        [Fact]
        public void Dashboard_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.Dashboard();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        // --- DeleteUser ---

        [Fact]
        public void DeleteUser_RemovesUser()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteUser(2);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Users.Find(2).Should().BeNull();
        }

        [Fact]
        public void DeleteUser_RemovesUserDonationsAndFunds()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            controller.DeleteUser(1); // Creator has funds 1 and 2

            context.Funds.Any(f => f.CreatorId == 1).Should().BeFalse();
        }

        [Fact]
        public void DeleteUser_DeniesNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.DeleteUser(2);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void DeleteUser_PreventsDeleteSelf()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteUser(3);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void DeleteUser_HandlesNonExistentUser()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteUser(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- ToggleAdmin ---

        [Fact]
        public void ToggleAdmin_TogglesFlag()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.ToggleAdmin(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            var user = context.Users.Find(1)!;
            user.IsAdmin.Should().BeTrue();
        }

        [Fact]
        public void ToggleAdmin_TogglesBack()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            controller.ToggleAdmin(1); // Make admin
            controller.ToggleAdmin(1); // Remove admin

            var user = context.Users.Find(1)!;
            user.IsAdmin.Should().BeFalse();
        }

        [Fact]
        public void ToggleAdmin_PreventsToggleSelf()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.ToggleAdmin(3);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void ToggleAdmin_DeniesNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.ToggleAdmin(2);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- DeleteFund ---

        [Fact]
        public void DeleteFund_RemovesFundAndDonations()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteFund(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Funds.Find(1).Should().BeNull();
            context.Donations.Any(d => d.FundId == 1).Should().BeFalse();
        }

        [Fact]
        public void DeleteFund_DeniesNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.DeleteFund(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void DeleteFund_HandlesNonExistentFund()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteFund(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- DeleteDonation ---

        [Fact]
        public void DeleteDonation_RemovesAndRefunds()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteDonation(1); // Donation of 100 to Fund 1

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
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.DeleteDonation(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void DeleteDonation_HandlesNonExistentDonation()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.DeleteDonation(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- EditFund ---

        [Fact]
        public void EditFund_UpdatesFundDetails()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.EditFund(1, "Updated Title", "Updated Description", 2000);

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
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.EditFund(1, "New", "New", 1000);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- AddFundsToFund ---

        [Fact]
        public void AddFundsToFund_IncreasesRaisedAmount()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.AddFundsToFund(1, 300);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Funds.Find(1)!.RaisedAmount.Should().Be(800); // 500 + 300
        }

        [Fact]
        public void AddFundsToFund_DeniesNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.AddFundsToFund(1, 300);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- ToggleVerified ---

        [Fact]
        public void ToggleVerified_TogglesFlag()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.ToggleVerified(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.Funds.Find(1)!.IsVerified.Should().BeTrue();
        }

        [Fact]
        public void ToggleVerified_TogglesBack()
        {
            var (controller, context) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            controller.ToggleVerified(1);
            controller.ToggleVerified(1);

            context.Funds.Find(1)!.IsVerified.Should().BeFalse();
        }

        [Fact]
        public void ToggleVerified_DeniesNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.ToggleVerified(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- GetStats ---

        [Fact]
        public void GetStats_ReturnsDataForAdmin()
        {
            var (controller, _) = CreateController(userId: 3, email: "admin@test.com", isAdmin: true);

            var result = controller.GetStats();

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        }

        [Fact]
        public void GetStats_DeniesNonAdmin()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.GetStats();

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }
    }
}
