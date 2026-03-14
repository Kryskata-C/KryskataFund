using FluentAssertions;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services;
using KryskataFund.Tests.Helpers;

namespace KryskataFund.Tests.Services
{
    public class DonationServiceTests
    {
        private (DonationService service, ApplicationDbContext context) CreateService()
        {
            var context = TestHelper.CreateDbContext();
            var service = new DonationService(context);
            return (service, context);
        }

        private (DonationService service, ApplicationDbContext context) CreateServiceWithSeededData()
        {
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var service = new DonationService(context);
            return (service, context);
        }

        // --- CreateAsync ---

        [Fact]
        public async Task CreateAsync_ValidDonation_PersistsToDatabase()
        {
            var (service, context) = CreateServiceWithSeededData();
            var donation = new Donation
            {
                FundId = 1,
                UserId = 2,
                DonorName = "@donor",
                Amount = 250
            };

            var result = await service.CreateAsync(donation);

            result.Id.Should().BeGreaterThan(0);
            context.Donations.Count().Should().Be(3); // 2 seeded + 1 new
        }

        [Fact]
        public async Task CreateAsync_AnonymousDonation_DefaultsDonorName()
        {
            var (service, context) = CreateService();

            // Add a fund first
            context.Funds.Add(new Fund
            {
                Id = 1, Title = "Test", Description = "Test", Category = "Test",
                GoalAmount = 1000, CreatorId = 1, CreatorName = "@test",
                EndDate = DateTime.UtcNow.AddDays(30)
            });
            await context.SaveChangesAsync();

            var donation = new Donation
            {
                FundId = 1,
                UserId = null,
                Amount = 50
            };

            var result = await service.CreateAsync(donation);

            result.DonorName.Should().Be("Anonymous");
        }

        [Fact]
        public async Task CreateAsync_MultipleDonations_AllPersisted()
        {
            var (service, context) = CreateService();

            for (int i = 0; i < 3; i++)
            {
                await service.CreateAsync(new Donation
                {
                    FundId = 1,
                    UserId = 1,
                    DonorName = "@user",
                    Amount = 100 + i * 10
                });
            }

            context.Donations.Count().Should().Be(3);
        }

        [Fact]
        public async Task CreateAsync_ReturnsDonationWithGeneratedId()
        {
            var (service, _) = CreateService();
            var donation = new Donation
            {
                FundId = 1,
                UserId = 1,
                DonorName = "@test",
                Amount = 75
            };

            var result = await service.CreateAsync(donation);

            result.Should().NotBeNull();
            result.Amount.Should().Be(75);
        }

        // --- GetByFundId ---

        [Fact]
        public void GetByFundId_FundWithDonations_ReturnsDonations()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByFundId(1).ToList();

            result.Should().HaveCount(2);
            result.Should().AllSatisfy(d => d.FundId.Should().Be(1));
        }

        [Fact]
        public void GetByFundId_FundWithNoDonations_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByFundId(2).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetByFundId_NonExistingFund_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByFundId(999).ToList();

            result.Should().BeEmpty();
        }

        // --- GetByUserId ---

        [Fact]
        public void GetByUserId_UserWithDonations_ReturnsDonations()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByUserId(2).ToList();

            result.Should().HaveCount(2);
            result.Should().AllSatisfy(d => d.UserId.Should().Be(2));
        }

        [Fact]
        public void GetByUserId_UserWithNoDonations_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByUserId(1).ToList();

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetByUserId_NonExistingUser_ReturnsEmpty()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByUserId(999).ToList();

            result.Should().BeEmpty();
        }

        // --- GetTotalDonated ---

        [Fact]
        public void GetTotalDonated_UserWithDonations_ReturnsSum()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTotalDonated(2);

            // Donation 1: 100, Donation 2: 50 => Total: 150
            result.Should().Be(150);
        }

        [Fact]
        public void GetTotalDonated_UserWithNoDonations_ReturnsZero()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTotalDonated(1);

            result.Should().Be(0);
        }

        [Fact]
        public void GetTotalDonated_NonExistingUser_ReturnsZero()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetTotalDonated(999);

            result.Should().Be(0);
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_ExistingDonation_RemovesFromDatabase()
        {
            var (service, context) = CreateServiceWithSeededData();

            await service.DeleteAsync(1);

            context.Donations.Count().Should().Be(1);
        }

        [Fact]
        public async Task DeleteAsync_NonExistingId_DoesNotThrow()
        {
            var (service, context) = CreateServiceWithSeededData();

            var act = () => service.DeleteAsync(999);

            await act.Should().NotThrowAsync();
            context.Donations.Count().Should().Be(2);
        }

        [Fact]
        public async Task DeleteAsync_ReducesCount()
        {
            var (service, context) = CreateServiceWithSeededData();
            var countBefore = context.Donations.Count();

            await service.DeleteAsync(1);

            context.Donations.Count().Should().Be(countBefore - 1);
        }

        [Fact]
        public async Task DeleteAsync_CorrectDonationRemoved()
        {
            var (service, context) = CreateServiceWithSeededData();

            await service.DeleteAsync(1);

            context.Donations.Find(1).Should().BeNull();
            context.Donations.Find(2).Should().NotBeNull();
        }
    }
}
