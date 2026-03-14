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

        // --- GetById ---

        [Fact]
        public void GetById_ExistingId_ReturnsFund()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetById(1);

            result.Should().NotBeNull();
            result!.Title.Should().Be("Test Fund 1");
            result.Category.Should().Be("Education");
        }

        [Fact]
        public void GetById_NonExistingId_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetById(999);

            result.Should().BeNull();
        }

        [Fact]
        public void GetById_EmptyDatabase_ReturnsNull()
        {
            var (service, _) = CreateService();

            var result = service.GetById(1);

            result.Should().BeNull();
        }

        // --- GetAll ---

        [Fact]
        public void GetAll_WithSeededData_ReturnsAllFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetAll().ToList();

            result.Should().HaveCount(3);
        }

        [Fact]
        public void GetAll_EmptyDatabase_ReturnsEmptyCollection()
        {
            var (service, _) = CreateService();

            var result = service.GetAll().ToList();

            result.Should().BeEmpty();
        }

        // --- GetByCategory ---

        [Fact]
        public void GetByCategory_ExistingCategory_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByCategory("Education").ToList();

            result.Should().HaveCount(1);
            result.First().Title.Should().Be("Test Fund 1");
        }

        [Fact]
        public void GetByCategory_CaseInsensitive_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByCategory("education").ToList();

            result.Should().HaveCount(1);
        }

        [Fact]
        public void GetByCategory_NonExistingCategory_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByCategory("Technology").ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetByCategory_HealthCategory_ReturnsSingleFund()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByCategory("Health").ToList();

            result.Should().HaveCount(1);
            result.First().GoalAmount.Should().Be(5000);
        }

        // --- Search ---

        [Fact]
        public void Search_ByTitle_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.Search("Test Fund 1").ToList();

            result.Should().HaveCount(1);
            result.First().Id.Should().Be(1);
        }

        [Fact]
        public void Search_ByDescription_ReturnsMatchingFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.Search("has ended").ToList();

            result.Should().HaveCount(1);
            result.First().Id.Should().Be(3);
        }

        [Fact]
        public void Search_PartialMatch_ReturnsAllMatching()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.Search("Test").ToList();

            result.Should().HaveCount(3); // all 3 have "Test" in title or description
        }

        [Fact]
        public void Search_NoMatch_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.Search("xyz_nonexistent").ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public void Search_CaseInsensitive_ReturnsResults()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.Search("test fund").ToList();

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
            var fund = service.GetById(1)!;
            fund.Title = "Updated Title";
            fund.GoalAmount = 9999;

            await service.UpdateAsync(fund);

            var updated = service.GetById(1);
            updated!.Title.Should().Be("Updated Title");
            updated.GoalAmount.Should().Be(9999);
        }

        [Fact]
        public async Task UpdateAsync_ChangeCategory_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var fund = service.GetById(2)!;
            fund.Category = "Sports";

            await service.UpdateAsync(fund);

            var updated = service.GetById(2);
            updated!.Category.Should().Be("Sports");
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_ExistingFund_RemovesFromDatabase()
        {
            var (service, context) = CreateServiceWithSeededData();

            await service.DeleteAsync(1);

            context.Funds.Should().HaveCount(2);
            service.GetById(1).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_NonExistingId_DoesNotThrow()
        {
            var (service, context) = CreateServiceWithSeededData();

            var act = () => service.DeleteAsync(999);

            await act.Should().NotThrowAsync();
            context.Funds.Should().HaveCount(3);
        }

        // --- GetTotalRaised ---

        [Fact]
        public void GetTotalRaised_WithSeededData_ReturnsSumOfRaisedAmounts()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTotalRaised();

            // Fund 1: 500, Fund 2: 0, Fund 3: 1500 => Total: 2000
            result.Should().Be(2000);
        }

        [Fact]
        public void GetTotalRaised_EmptyDatabase_ReturnsZero()
        {
            var (service, _) = CreateService();

            var result = service.GetTotalRaised();

            result.Should().Be(0);
        }

        // --- GetActiveCampaignCount ---

        [Fact]
        public void GetActiveCampaignCount_WithSeededData_ReturnsCountOfActiveFunds()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetActiveCampaignCount();

            // Fund 1: +30 days, Fund 2: +60 days, Fund 3: -1 day (ended)
            result.Should().Be(2);
        }

        [Fact]
        public void GetActiveCampaignCount_EmptyDatabase_ReturnsZero()
        {
            var (service, _) = CreateService();

            var result = service.GetActiveCampaignCount();

            result.Should().Be(0);
        }

        // --- GetTopFunded ---

        [Fact]
        public void GetTopFunded_ReturnsRequestedCount()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTopFunded(2).ToList();

            result.Should().HaveCount(2);
        }

        [Fact]
        public void GetTopFunded_OrderedByRaisedAmountDescending()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTopFunded(3).ToList();

            result[0].RaisedAmount.Should().Be(1500); // Fund 3
            result[1].RaisedAmount.Should().Be(500);  // Fund 1
            result[2].RaisedAmount.Should().Be(0);    // Fund 2
        }

        [Fact]
        public void GetTopFunded_RequestMoreThanExist_ReturnsAll()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTopFunded(10).ToList();

            result.Should().HaveCount(3);
        }

        [Fact]
        public void GetTopFunded_EmptyDatabase_ReturnsEmpty()
        {
            var (service, _) = CreateService();

            var result = service.GetTopFunded(5).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetTopFunded_ZeroCount_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTopFunded(0).ToList();

            result.Should().BeEmpty();
        }
    }
}
