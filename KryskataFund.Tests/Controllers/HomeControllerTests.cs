using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Models;
using KryskataFund.ViewModels;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace KryskataFund.Tests.Controllers
{
    public class HomeControllerTests
    {
        private HomeController CreateController(string? dbName = null)
        {
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);
            var logger = new Mock<ILogger<HomeController>>();
            var controller = new HomeController(logger.Object, context);
            TestHelper.SetupSession(controller);
            return controller;
        }

        [Fact]
        public void Index_ReturnsViewResult_WithFunds()
        {
            var controller = CreateController();

            var result = controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<HomeViewModel>().Subject;
            model.Funds.Should().HaveCount(3);
            model.Funds.Should().Contain(f => f.Title == "Test Fund 1" && f.Category == "Education");
            model.Funds.Should().Contain(f => f.Title == "Test Fund 2" && f.Category == "Health");
            model.Funds.Should().Contain(f => f.Title == "Ended Fund" && f.Category == "Animals");
        }

        [Fact]
        public void Index_WithCategory_FiltersCorrectly()
        {
            var controller = CreateController();

            var result = controller.Index("Education");

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<HomeViewModel>().Subject;
            model.Funds.Should().HaveCount(1);
            model.Funds[0].Category.Should().Be("Education");
        }

        [Fact]
        public void Index_WithNullCategory_ReturnsAllFunds()
        {
            var controller = CreateController();

            var result = controller.Index(null);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<HomeViewModel>().Subject;
            model.Funds.Should().HaveCount(3);
            model.Funds.Select(f => f.Category).Should().BeEquivalentTo(new[] { "Education", "Health", "Animals" });
        }

        [Fact]
        public void Index_WithNonExistentCategory_ReturnsEmpty()
        {
            var controller = CreateController();

            var result = controller.Index("NonExistent");

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<HomeViewModel>().Subject;
            model.Funds.Should().BeEmpty();
        }

        [Fact]
        public void Index_SetsViewBagCategoryCounts()
        {
            var controller = CreateController();

            var result = controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<HomeViewModel>().Subject;
            model.CategoryCounts.Should().ContainKey("Education");
            model.CategoryCounts.Should().ContainKey("Health");
            model.CategoryCounts.Should().ContainKey("Animals");
        }

        [Fact]
        public void Index_WhenSignedIn_LoadsFollowedFundIds()
        {
            var dbName = Guid.NewGuid().ToString();
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);
            context.UserFollows.Add(new UserFollow { UserId = 2, FundId = 1, FollowedAt = DateTime.UtcNow });
            context.SaveChanges();

            var logger = new Mock<ILogger<HomeController>>();
            var controller = new HomeController(logger.Object, context);
            TestHelper.SetupSession(controller, userId: 2, email: "donor@test.com");

            var result = controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<HomeViewModel>().Subject;
            model.FollowedFundIds.Should().Contain(1);
        }

        [Fact]
        public void Leaderboard_ReturnsViewResult()
        {
            var controller = CreateController();

            var result = controller.Leaderboard();

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Leaderboard_SetsViewBagStats()
        {
            var controller = CreateController();

            controller.Leaderboard();

            ((decimal)controller.ViewBag.TotalRaised).Should().Be(2000);
            ((int)controller.ViewBag.TotalDonations).Should().Be(2);
            ((int)controller.ViewBag.TotalCampaigns).Should().Be(3);
            ((int)controller.ViewBag.TotalUsers).Should().Be(3);
        }

        [Fact]
        public void Privacy_ReturnsViewResult()
        {
            var controller = CreateController();

            var result = controller.Privacy();

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void GetRecentActivity_ReturnsJsonResult()
        {
            var controller = CreateController();

            var result = controller.GetRecentActivity();

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public void GetLiveStats_ReturnsJsonResult()
        {
            var controller = CreateController();

            var result = controller.GetLiveStats();

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public void Error_ReturnsViewWithErrorViewModel()
        {
            var controller = CreateController();

            var result = controller.Error();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.Model.Should().BeOfType<ErrorViewModel>();
        }
    }
}
