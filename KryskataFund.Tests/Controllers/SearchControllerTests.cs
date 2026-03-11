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
        public void Index_WithNullQuery_ReturnsEmptyResults()
        {
            var controller = CreateController();

            var result = controller.Index(null);

            result.Should().BeOfType<ViewResult>();
            ((string)controller.ViewBag.Query).Should().BeEmpty();
        }

        [Fact]
        public void Index_WithEmptyQuery_ReturnsEmptyResults()
        {
            var controller = CreateController();

            var result = controller.Index("  ");

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Index_WithMatchingQuery_ReturnsResults()
        {
            var controller = CreateController();

            var result = controller.Index("Test Fund 1");

            result.Should().BeOfType<ViewResult>();
            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().NotBeEmpty();
        }

        [Fact]
        public void Index_SearchesByCategory()
        {
            var controller = CreateController();

            var result = controller.Index("Education");

            result.Should().BeOfType<ViewResult>();
            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().HaveCount(1);
        }

        [Fact]
        public void Index_SearchesByCreatorName()
        {
            var controller = CreateController();

            var result = controller.Index("@creator");

            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().HaveCount(2);
        }

        [Fact]
        public void Index_NoMatch_ReturnsEmpty()
        {
            var controller = CreateController();

            controller.Index("zzzznonexistent");

            var results = (List<KryskataFund.Models.Fund>)controller.ViewBag.Results;
            results.Should().BeEmpty();
        }

        [Fact]
        public void Autocomplete_WithShortTerm_ReturnsEmpty()
        {
            var controller = CreateController();

            var result = controller.Autocomplete("a");

            var json = result.Should().BeOfType<JsonResult>().Subject;
        }

        [Fact]
        public void Autocomplete_WithNullTerm_ReturnsEmpty()
        {
            var controller = CreateController();

            var result = controller.Autocomplete(null);

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public void Autocomplete_WithValidTerm_ReturnsResults()
        {
            var controller = CreateController();

            var result = controller.Autocomplete("Test");

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public void Autocomplete_LimitsToFiveResults()
        {
            var controller = CreateController();

            var result = controller.Autocomplete("Fund");

            result.Should().BeOfType<JsonResult>();
        }
    }
}
