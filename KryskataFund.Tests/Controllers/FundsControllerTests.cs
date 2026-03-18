using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace KryskataFund.Tests.Controllers
{
    public class FundsControllerTests
    {
        private (FundsController controller, ApplicationDbContext context) CreateController(string? dbName = null, int? userId = null, string? email = null, bool isAdmin = false)
        {
            dbName ??= Guid.NewGuid().ToString();
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);

            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var mockConfig = new Mock<IConfiguration>();

            var mockEmailService = new Mock<KryskataFund.Services.Interfaces.IEmailService>();
            var controller = new FundsController(context, mockEnv.Object, mockConfig.Object, mockEmailService.Object);
            TestHelper.SetupSession(controller, userId, email, isAdmin);
            return (controller, context);
        }

        // --- Create GET ---

        [Fact]
        public void Create_Get_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.Create();

            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("SignIn");
            redirect.ControllerName.Should().Be("Account");
        }

        [Fact]
        public void Create_Get_ReturnsViewWhenSignedIn()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.Create();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeOfType<CreateFundViewModel>();
        }

        // --- Details ---

        [Fact]
        public void Details_ReturnsViewForExistingFund()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = controller.Details(1);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var fund = viewResult.Model.Should().BeOfType<Fund>().Subject;
            fund.Title.Should().Be("Test Fund 1");
        }

        [Fact]
        public void Details_ReturnsNotFoundForInvalidId()
        {
            var (controller, _) = CreateController(userId: 1);

            var result = controller.Details(999);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public void Details_SetsIsCreatorForCreator()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            controller.Details(1);

            ((bool)controller.ViewBag.IsCreator).Should().BeTrue();
            ((bool)controller.ViewBag.IsOriginalCreator).Should().BeTrue();
        }

        [Fact]
        public void Details_SetsIsCreatorFalseForNonCreator()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            controller.Details(1);

            ((bool)controller.ViewBag.IsOriginalCreator).Should().BeFalse();
        }

        // --- Embed ---

        [Fact]
        public void Embed_ReturnsViewForExistingFund()
        {
            var (controller, _) = CreateController();

            var result = controller.Embed(1);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeOfType<Fund>();
        }

        [Fact]
        public void Embed_ReturnsNotFoundForInvalidId()
        {
            var (controller, _) = CreateController();

            var result = controller.Embed(999);

            result.Should().BeOfType<NotFoundResult>();
        }

        // --- Donate ---

        [Fact]
        public void Donate_RedirectsWhenNotSignedIn()
        {
            var (controller, _) = CreateController();

            var result = controller.Donate(1, 50);

            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void Donate_ReturnsViewForSignedInUser()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = controller.Donate(1, 50);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeOfType<Fund>();
        }

        [Fact]
        public void Donate_ReturnsNotFoundForInvalidFund()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = controller.Donate(999, 50);

            result.Should().BeOfType<NotFoundResult>();
        }

        // --- ProcessDonation ---

        [Fact]
        public async Task ProcessDonation_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.ProcessDonation(1, 100);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task ProcessDonation_UpdatesFundAmounts()
        {
            var (controller, context) = CreateController(userId: 2, email: "donor@test.com");

            await controller.ProcessDonation(1, 200);

            var fund = context.Funds.Find(1)!;
            fund.RaisedAmount.Should().Be(700); // 500 + 200
            fund.SupportersCount.Should().Be(6); // 5 + 1
        }

        [Fact]
        public async Task ProcessDonation_CreatesDonationRecord()
        {
            var (controller, context) = CreateController(userId: 2, email: "donor@test.com");

            await controller.ProcessDonation(1, 200);

            context.Donations.Count(d => d.FundId == 1).Should().Be(3); // 2 existing + 1 new
        }

        [Fact]
        public async Task ProcessDonation_ReturnsNotFoundForInvalidFund()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.ProcessDonation(999, 100);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task ProcessDonation_AutoMarksMilestonesAsReached()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.FundMilestones.Add(new FundMilestone
            {
                FundId = 1, Title = "Half way", TargetAmount = 600, IsReached = false
            });
            context.SaveChanges();

            await controller.ProcessDonation(1, 200); // raised goes to 700

            var milestone = context.FundMilestones.First(m => m.FundId == 1);
            milestone.IsReached.Should().BeTrue();
        }

        // --- PostUpdate ---

        [Fact]
        public async Task PostUpdate_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.PostUpdate(1, "Update", "Content");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task PostUpdate_AllowsCreator()
        {
            var (controller, context) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.PostUpdate(1, "Update Title", "Update Content");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.FundUpdates.Count(u => u.FundId == 1).Should().Be(1);
        }

        [Fact]
        public async Task PostUpdate_DeniesNonCreator()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.PostUpdate(1, "Update", "Content");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task PostUpdate_AllowsCollaborator()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.FundCollaborators.Add(new FundCollaborator { FundId = 1, UserId = 2, Role = "collaborator" });
            context.SaveChanges();

            var result = await controller.PostUpdate(1, "Collab Update", "Content");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        }

        // --- AddMilestone ---

        [Fact]
        public async Task AddMilestone_CreatesForCreator()
        {
            var (controller, context) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.AddMilestone(1, "Milestone 1", 750, "A milestone");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.FundMilestones.Count(m => m.FundId == 1).Should().Be(1);
        }

        [Fact]
        public async Task AddMilestone_DeniesNonCreator()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.AddMilestone(1, "Milestone", 500, null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task AddMilestone_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.AddMilestone(1, "Milestone", 500, null);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task AddMilestone_MarksAsReachedIfAlreadyMet()
        {
            var (controller, context) = CreateController(userId: 1, email: "creator@test.com");

            await controller.AddMilestone(1, "Easy milestone", 100, null); // Fund has 500 raised

            var milestone = context.FundMilestones.First();
            milestone.IsReached.Should().BeTrue();
        }

        // --- DeleteMilestone ---

        [Fact]
        public async Task DeleteMilestone_RemovesForCreator()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");
            context.FundMilestones.Add(new FundMilestone { Id = 10, FundId = 1, Title = "M1", TargetAmount = 500 });
            context.SaveChanges();

            var result = await controller.DeleteMilestone(10);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.FundMilestones.Any(m => m.Id == 10).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteMilestone_DeniesNonCreator()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.FundMilestones.Add(new FundMilestone { Id = 10, FundId = 1, Title = "M1", TargetAmount = 500 });
            context.SaveChanges();

            var result = await controller.DeleteMilestone(10);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- RequestExtension ---

        [Fact]
        public async Task RequestExtension_ExtendsDeadlineForCreator()
        {
            var (controller, context) = CreateController(userId: 2, email: "donor@test.com");
            // Fund 3 ended yesterday, creator is userId=2
            var result = await controller.RequestExtension(3, 7, "Need more time to reach goal");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.DeadlineExtensions.Any(e => e.FundId == 3).Should().BeTrue();
        }

        [Fact]
        public async Task RequestExtension_DeniesNonCreator()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.RequestExtension(3, 7, "Need more time");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_ValidatesDayRange()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.RequestExtension(3, 31, "Too many days");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_DeniesWhenMoreThan7DaysLeft()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.RequestExtension(1, 7, "Fund still has time"); // Fund 1 has 30 days left

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_DeniesSecondExtension()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.DeadlineExtensions.Add(new DeadlineExtension
            {
                FundId = 3, OriginalEndDate = DateTime.UtcNow.AddDays(-1), NewEndDate = DateTime.UtcNow.AddDays(6),
                ExtensionDays = 7, Reason = "First extension"
            });
            context.SaveChanges();

            var result = await controller.RequestExtension(3, 5, "Second extension");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- CreateRecurringDonation ---

        [Fact]
        public async Task CreateRecurringDonation_CreatesRecord()
        {
            var (controller, context) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.CreateRecurringDonation(1, 25);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.RecurringDonations.Count(r => r.FundId == 1 && r.UserId == 2).Should().Be(1);
        }

        [Fact]
        public async Task CreateRecurringDonation_DeniesDuplicate()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.RecurringDonations.Add(new RecurringDonation
            {
                FundId = 1, UserId = 2, DonorName = "@donor", Amount = 10, IsActive = true
            });
            context.SaveChanges();

            var result = await controller.CreateRecurringDonation(1, 25);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task CreateRecurringDonation_RequiresSignIn()
        {
            var (controller, _) = CreateController();

            var result = await controller.CreateRecurringDonation(1, 25);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- AddCollaborator ---

        [Fact]
        public async Task AddCollaborator_AddsUserAsCollaborator()
        {
            var (controller, context) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.AddCollaborator(1, "donor@test.com");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
            context.FundCollaborators.Any(c => c.FundId == 1 && c.UserId == 2).Should().BeTrue();
        }

        [Fact]
        public async Task AddCollaborator_DeniesNonCreator()
        {
            var (controller, _) = CreateController(userId: 2, email: "donor@test.com");

            var result = await controller.AddCollaborator(1, "admin@test.com");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task AddCollaborator_RejectsNonExistentUser()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.AddCollaborator(1, "nobody@test.com");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task AddCollaborator_RejectsSelf()
        {
            var (controller, _) = CreateController(userId: 1, email: "creator@test.com");

            var result = await controller.AddCollaborator(1, "creator@test.com");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task AddCollaborator_RejectsDuplicate()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");
            context.FundCollaborators.Add(new FundCollaborator { FundId = 1, UserId = 2, Role = "collaborator" });
            context.SaveChanges();

            var result = await controller.AddCollaborator(1, "donor@test.com");

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        // --- RemoveCollaborator ---

        [Fact]
        public async Task RemoveCollaborator_RemovesForCreator()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 1, email: "creator@test.com");
            context.FundCollaborators.Add(new FundCollaborator { Id = 10, FundId = 1, UserId = 2, Role = "collaborator" });
            context.SaveChanges();

            var result = await controller.RemoveCollaborator(10);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(true);
        }

        [Fact]
        public async Task RemoveCollaborator_DeniesNonCreator()
        {
            var dbName = Guid.NewGuid().ToString();
            var (controller, context) = CreateController(dbName: dbName, userId: 2, email: "donor@test.com");
            context.FundCollaborators.Add(new FundCollaborator { Id = 10, FundId = 1, UserId = 2, Role = "collaborator" });
            context.SaveChanges();

            var result = await controller.RemoveCollaborator(10);

            var json = result.Should().BeOfType<JsonResult>().Subject;
            var value = json.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }
    }
}
