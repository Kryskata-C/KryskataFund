using FluentAssertions;
using KryskataFund.Controllers;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace KryskataFund.Tests.Controllers
{
    public class SearchControllerTests
    {
        private SearchController CreateController(string? dbName = null)
        {
            var context = TestHelper.CreateDbContext(dbName);
            TestHelper.SeedTestData(context);
            var controller = new SearchController(context);
            TestHelper.SetupSession(controller);
            return controller;
        }

        [Fact]
        public async Task Index_WithNullQuery_ReturnsEmptyResults()
        {
            var controller = CreateController();

            var result = await controller.Index(null);

            result.Should().BeOfType<ViewResult>();
            ((string)controller.ViewBag.Query).Should().BeEmpty();
        }

        [Fact]
        public async Task Index_WithEmptyQuery_ReturnsEmptyResults()
        {
            var controller = CreateController();

            var result = await controller.Index("  ");

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Index_WithMatchingQuery_ReturnsResults()
        {
            var controller = CreateController();

            var result = await controller.Index("Test Fund 1");

            result.Should().BeOfType<ViewResult>();
            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Index_SearchesByCategory()
        {
            var controller = CreateController();

            var result = await controller.Index("Education");

            result.Should().BeOfType<ViewResult>();
            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().HaveCount(1);
        }

        [Fact]
        public async Task Index_SearchesByCreatorName()
        {
            var controller = CreateController();

            var result = await controller.Index("@creator");

            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().HaveCount(2);
        }

        [Fact]
        public async Task Index_NoMatch_ReturnsEmpty()
        {
            var controller = CreateController();

            await controller.Index("zzzznonexistent");

            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task Autocomplete_WithShortTerm_ReturnsEmpty()
        {
            var controller = CreateController();

            var result = await controller.Autocomplete("a");

            var json = result.Should().BeOfType<JsonResult>().Subject;
        }

        [Fact]
        public async Task Autocomplete_WithNullTerm_ReturnsEmpty()
        {
            var controller = CreateController();

            var result = await controller.Autocomplete(null);

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public async Task Autocomplete_WithValidTerm_ReturnsResults()
        {
            var controller = CreateController();

            var result = await controller.Autocomplete("Test");

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public async Task Autocomplete_LimitsToFiveResults()
        {
            var controller = CreateController();

            var result = await controller.Autocomplete("Fund");

            result.Should().BeOfType<JsonResult>();
        }
    }
}
