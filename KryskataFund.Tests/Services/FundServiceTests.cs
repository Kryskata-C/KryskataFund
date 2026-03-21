using FluentAssertions;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services;
using KryskataFund.Tests.Helpers;

namespace KryskataFund.Tests.Services
{
    public class FundServiceTests
    {
        private (FundService service, ApplicationDbContext context) CreateService()
        {
            var context = TestHelper.CreateDbContext();
            var service = new FundService(context);
            return (service, context);
        }

        private (FundService service, ApplicationDbContext context) CreateServiceWithSeededData()
        {
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var service = new FundService(context);
            return (service, context);
        }

        // --- GetByIdAsync ---

        [Fact]
        public async Task GetById_ExistingId_ReturnsFund()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByIdAsync(1);

            result.Should().NotBeNull();
            result!.Title.Should().Be("Test Fund 1");
            result.Category.Should().Be("Education");
        }

        [Fact]
        public async Task GetById_NonExistingId_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByIdAsync(999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetById_EmptyDatabase_ReturnsNull()
        {
            var (service, _) = CreateService();

            var result = await service.GetByIdAsync(1);

            result.Should().BeNull();
        }

        // --- GetAllAsync ---

        [Fact]
        public async Task GetAll_WithSeededData_ReturnsAllFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetAllAsync()).ToList();

            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetAll_EmptyDatabase_ReturnsEmptyCollection()
        {
            var (service, _) = CreateService();

            var result = (await service.GetAllAsync()).ToList();

            result.Should().BeEmpty();
        }

        // --- GetByCategoryAsync ---

        [Fact]
        public async Task GetByCategory_ExistingCategory_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetByCategoryAsync("Education")).ToList();

            result.Should().HaveCount(1);
            result.First().Title.Should().Be("Test Fund 1");
        }

        [Fact]
        public async Task GetByCategory_CaseInsensitive_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetByCategoryAsync("education")).ToList();

            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetByCategory_NonExistingCategory_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetByCategoryAsync("Technology")).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByCategory_HealthCategory_ReturnsSingleFund()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetByCategoryAsync("Health")).ToList();

            result.Should().HaveCount(1);
            result.First().GoalAmount.Should().Be(5000);
        }

        // --- SearchAsync ---

        [Fact]
        public async Task Search_ByTitle_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.SearchAsync("Test Fund 1")).ToList();

            result.Should().HaveCount(1);
            result.First().Id.Should().Be(1);
        }

        [Fact]
        public async Task Search_ByDescription_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.SearchAsync("has ended")).ToList();

            result.Should().HaveCount(1);
            result.First().Id.Should().Be(3);
        }

        [Fact]
        public async Task Search_PartialMatch_ReturnsAllMatching()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.SearchAsync("Test")).ToList();

            result.Should().HaveCountGreaterThanOrEqualTo(2); // Funds with "Test" in title/description
        }

        [Fact]
        public async Task Search_NoMatch_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.SearchAsync("xyz_nonexistent")).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Search_CaseInsensitive_ReturnsResults()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.SearchAsync("test fund")).ToList();

            result.Should().HaveCountGreaterThan(0);
        }

        // --- CreateAsync ---

        [Fact]
        public async Task CreateAsync_ValidFund_PersistsToDatabase()
        {
            var (service, context) = CreateService();
            var fund = new Fund
            {
                Title = "New Fund",
                Description = "New Description",
                Category = "Technology",
                GoalAmount = 3000,
                CreatorId = 1,
                CreatorName = "@testuser",
                EndDate = DateTime.UtcNow.AddDays(30)
            };

            var result = await service.CreateAsync(fund);

            result.Id.Should().BeGreaterThan(0);
            context.Funds.Should().HaveCount(1);
            context.Funds.First().Title.Should().Be("New Fund");
        }

        [Fact]
        public async Task CreateAsync_MultipleFunds_AllPersisted()
        {
            var (service, context) = CreateService();

            for (int i = 0; i < 5; i++)
            {
                await service.CreateAsync(new Fund
                {
                    Title = $"Fund {i}",
                    Description = "Desc",
                    Category = "Test",
                    GoalAmount = 1000,
                    CreatorId = 1,
                    CreatorName = "@user",
                    EndDate = DateTime.UtcNow.AddDays(30)
                });
            }

            context.Funds.Should().HaveCount(5);
        }

        // --- UpdateAsync ---

        [Fact]
        public async Task UpdateAsync_ExistingFund_UpdatesValues()
        {
            var (service, context) = CreateServiceWithSeededData();
            var fund = (await service.GetByIdAsync(1))!;
            fund.Title = "Updated Title";
            fund.GoalAmount = 9999;

            await service.UpdateAsync(fund);

            var updated = await service.GetByIdAsync(1);
            updated!.Title.Should().Be("Updated Title");
            updated.GoalAmount.Should().Be(9999);
        }

        [Fact]
        public async Task UpdateAsync_ChangeCategory_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var fund = (await service.GetByIdAsync(2))!;
            fund.Category = "Sports";

            await service.UpdateAsync(fund);

            var updated = await service.GetByIdAsync(2);
            updated!.Category.Should().Be("Sports");
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_ExistingFund_RemovesFromDatabase()
        {
            var (service, context) = CreateServiceWithSeededData();

            await service.DeleteAsync(1);

            context.Funds.Should().HaveCount(2);
            (await service.GetByIdAsync(1)).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_NonExistingId_DoesNotThrow()
        {
            var (service, context) = CreateServiceWithSeededData();

            var act = () => service.DeleteAsync(999);

            await act.Should().NotThrowAsync();
            context.Funds.Should().HaveCount(3);
        }

        // --- GetTotalRaisedAsync ---

        [Fact]
        public async Task GetTotalRaised_WithSeededData_ReturnsSumOfRaisedAmounts()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetTotalRaisedAsync();

            // Fund 1: 500, Fund 2: 0, Fund 3: 1500 => Total: 2000
            result.Should().Be(2000);
        }

        [Fact]
        public async Task GetTotalRaised_EmptyDatabase_ReturnsZero()
        {
            var (service, _) = CreateService();

            var result = await service.GetTotalRaisedAsync();

            result.Should().Be(0);
        }

        // --- GetActiveCampaignCountAsync ---

        [Fact]
        public async Task GetActiveCampaignCount_WithSeededData_ReturnsCountOfActiveFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetActiveCampaignCountAsync();

            // Fund 1: +30 days, Fund 2: +60 days, Fund 3: -1 day (ended)
            result.Should().Be(2);
        }

        [Fact]
        public async Task GetActiveCampaignCount_EmptyDatabase_ReturnsZero()
        {
            var (service, _) = CreateService();

            var result = await service.GetActiveCampaignCountAsync();

            result.Should().Be(0);
        }

        // --- GetTopFundedAsync ---

        [Fact]
        public async Task GetTopFunded_ReturnsRequestedCount()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetTopFundedAsync(2)).ToList();

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetTopFunded_OrderedByRaisedAmountDescending()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetTopFundedAsync(3)).ToList();

            result[0].RaisedAmount.Should().Be(1500); // Fund 3
            result[1].RaisedAmount.Should().Be(500);  // Fund 1
            result[2].RaisedAmount.Should().Be(0);    // Fund 2
        }

        [Fact]
        public async Task GetTopFunded_RequestMoreThanExist_ReturnsAll()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetTopFundedAsync(10)).ToList();

            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetTopFunded_EmptyDatabase_ReturnsEmpty()
        {
            var (service, _) = CreateService();

            var result = (await service.GetTopFundedAsync(5)).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTopFunded_ZeroCount_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = (await service.GetTopFundedAsync(0)).ToList();

            result.Should().BeEmpty();
        }
    }
}
