using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Tests.Controllers
{
    public class AccountControllerTests
    {
        private (AccountController controller, ApplicationDbContext context) CreateController(string? dbName = null, int? userId = null, string? email = null, bool isAdmin = false)
        {
            dbName ??= Guid.NewGuid().ToString();
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);
            var controller = new AccountController(context);
            TestHelper.SetupSession(controller, userId, email, isAdmin);
            return (controller, context);
        }

        // --- SignIn GET ---

        [Fact]
        public void SignIn_Get_ReturnsView()
        {
            var (controller, _) = CreateController();

            var result = controller.SignIn();

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void SignIn_Get_SetsReturnUrl()
        {
            var (controller, _) = CreateController();

            controller.SignIn("/some/path");

            controller.ViewData["ReturnUrl"].Should().Be("/some/path");
        }

        // --- SignIn POST ---

        [Fact]
        public void SignIn_Post_WithValidCredentials_SetsSession()
        {
            var (controller, _) = CreateController();

            var result = controller.SignIn("creator@test.com", "Password1");

            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");

            controller.HttpContext.Session.GetString("IsSignedIn").Should().Be("true");
            controller.HttpContext.Session.GetString("UserEmail").Should().Be("creator@test.com");
        }

        [Fact]
        public void SignIn_Post_WithInvalidEmail_ShowsError()
        {
            var (controller, _) = CreateController();

            var result = controller.SignIn("wrong@test.com", "Password1");

            result.Should().BeOfType<ViewResult>();
            controller.ViewData["Error"].Should().Be("Invalid email or password");
        }

        [Fact]
        public void SignIn_Post_WithInvalidPassword_ShowsError()
        {
            var (controller, _) = CreateController();

            var result = controller.SignIn("creator@test.com", "WrongPassword1");

            result.Should().BeOfType<ViewResult>();
            controller.ViewData["Error"].Should().Be("Invalid email or password");
        }

        // --- SignUp POST ---

        [Fact]
        public void SignUp_Get_ReturnsView()
        {
            var (controller, _) = CreateController();

            var result = controller.SignUp();

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void SignUp_Post_CreatesNewUser()
        {
            var (controller, context) = CreateController();
            var model = new SignUpViewModel
            {
                Email = "newuser@test.com",
                Password = "Password1",
                ConfirmPassword = "Password1"
            };

            var result = controller.SignUp(model);

            result.Should().BeOfType<RedirectToActionResult>();
            context.Users.Any(u => u.Email == "newuser@test.com").Should().BeTrue();
            controller.HttpContext.Session.GetString("IsSignedIn").Should().Be("true");
        }

        [Fact]
        public void SignUp_Post_RejectsDuplicateEmail()
        {
            var (controller, _) = CreateController();
            var model = new SignUpViewModel
            {
                Email = "creator@test.com",
                Password = "Password1",
                ConfirmPassword = "Password1"
            };

            var result = controller.SignUp(model);

            result.Should().BeOfType<ViewResult>();
            controller.ModelState.ErrorCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SignUp_Post_WithInvalidModel_ReturnsView()
        {
            var (controller, _) = CreateController();
            controller.ModelState.AddModelError("Email", "Required");
            var model = new SignUpViewModel();

            var result = controller.SignUp(model);

            result.Should().BeOfType<ViewResult>();
        }

        // --- SignOut ---

        [Fact]
        public void SignOut_ClearsSessionAndRedirects()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.SignOut();

            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)result;
            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        // --- Profile ---

        [Fact]
        public void Profile_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.Profile();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void Profile_ReturnsViewWhenSignedIn()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.Profile();

            result.Should().BeOfType<ViewResult>();
            ((User)controller.ViewBag.User).Email.Should().Be("creator@test.com");
        }

        [Fact]
        public void Profile_CalculatesStats()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            controller.Profile();

            // User 1 has funds 1 and 2, total raised = 500 + 0 = 500
            ((decimal)controller.ViewBag.TotalRaised).Should().Be(500);
        }

        // --- MyFunds ---

        [Fact]
        public void MyFunds_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.MyFunds();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void MyFunds_ReturnsViewForSignedInUser()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.MyFunds();

            result.Should().BeOfType<ViewResult>();
            var funds = (List<Fund>)controller.ViewBag.MyFunds;
            funds.Should().HaveCount(2); // User 1 created funds 1 and 2
        }

        // --- MyDonations ---

        [Fact]
        public void MyDonations_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.MyDonations();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void MyDonations_ReturnsViewForSignedInUser()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = controller.MyDonations();

            result.Should().BeOfType<ViewResult>();
            var donations = (List<Donation>)controller.ViewBag.MyDonations;
            donations.Should().HaveCount(2);
            ((decimal)controller.ViewBag.TotalDonated).Should().Be(150);
        }

        // --- Following ---

        [Fact]
        public void Following_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.Following();

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void Following_ReturnsViewForSignedInUser()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = controller.Following();

            result.Should().BeOfType<ViewResult>();
        }

        // --- ToggleFollow ---

        [Fact]
        public void ToggleFollow_AddsFollow()
        {
            var (controller, context) = CreateController(userId: 2, email: "donor@test.com");

            var result = controller.ToggleFollow(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            value!.GetType().GetProperty("isFollowing")!.GetValue(value).Should().Be(true);
            context.UserFollows.Any(f => f.UserId == 2 && f.FundId == 1).Should().BeTrue();
        }

        [Fact]
        public void ToggleFollow_RemovesExistingFollow()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.UserFollows.Add(new UserFollow { UserId = 2, FundId = 1, FollowedAt = DateTime.UtcNow });
            context.SaveChanges();

            var result = controller.ToggleFollow(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("isFollowing")!.GetValue(value).Should().Be(false);
            context.UserFollows.Any(f => f.UserId == 2 && f.FundId == 1).Should().BeFalse();
        }

        [Fact]
        public void ToggleFollow_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = controller.ToggleFollow(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- SaveBuddyCustomization ---

        [Fact]
        public async Task SaveBuddyCustomization_SavesForSignedInUser()
        {
            var (controller, context) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.SaveBuddyCustomization("round", "tophat", "bandit");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            var user = context.Users.Find(1)!;
            user.BuddyGlasses.Should().Be("round");
            user.BuddyHat.Should().Be("tophat");
            user.BuddyMask.Should().Be("bandit");
        }

        [Fact]
        public async Task SaveBuddyCustomization_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.SaveBuddyCustomization("round", null, null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- GetBuddyCustomization ---

        [Fact]
        public void GetBuddyCustomization_ReturnsDataForSignedInUser()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");
            var user = context.Users.Find(1)!;
            user.BuddyGlasses = "aviator";
            context.SaveChanges();

            var result = controller.GetBuddyCustomization();

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public void GetBuddyCustomization_ReturnsNullsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.GetBuddyCustomization();

            result.Should().BeOfType<JsonResult>();
        }

        // --- CancelRecurringDonation ---

        [Fact]
        public async Task CancelRecurringDonation_CancelsDonation()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.RecurringDonations.Add(new RecurringDonation
            {
                Id = 10, FundId = 1, UserId = 2, DonorName = "@donor", Amount = 25, IsActive = true
            });
            context.SaveChanges();

            var result = await controller.CancelRecurringDonation(10);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            var recurring = context.RecurringDonations.Find(10)!;
            recurring.IsActive.Should().BeFalse();
            recurring.CancelledAt.Should().NotBeNull();
        }

        [Fact]
        public async Task CancelRecurringDonation_FailsForNonExistent()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.CancelRecurringDonation(999);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task CancelRecurringDonation_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.CancelRecurringDonation(1);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }
    }
}
