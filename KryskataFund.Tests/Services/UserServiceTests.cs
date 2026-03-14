using FluentAssertions;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services;
using KryskataFund.Tests.Helpers;

namespace KryskataFund.Tests.Services
{
    public class UserServiceTests
    {
        private (UserService service, ApplicationDbContext context) CreateService()
        {
            var context = TestHelper.CreateDbContext();
            var service = new UserService(context);
            return (service, context);
        }

        private (UserService service, ApplicationDbContext context) CreateServiceWithSeededData()
        {
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var service = new UserService(context);
            return (service, context);
        }

        // --- GetById ---

        [Fact]
        public void GetById_ExistingUser_ReturnsUser()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetById(1);

            result.Should().NotBeNull();
            result!.Email.Should().Be("creator@test.com");
        }

        [Fact]
        public void GetById_NonExistingUser_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetById(999);

            result.Should().BeNull();
        }

        [Fact]
        public void GetById_AdminUser_HasIsAdminTrue()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetById(3);

            result.Should().NotBeNull();
            result!.IsAdmin.Should().BeTrue();
        }

        // --- GetByEmail ---

        [Fact]
        public void GetByEmail_ExistingEmail_ReturnsUser()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByEmail("creator@test.com");

            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
        }

        [Fact]
        public void GetByEmail_NonExistingEmail_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByEmail("nonexistent@test.com");

            result.Should().BeNull();
        }

        [Fact]
        public void GetByEmail_EmptyString_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.GetByEmail("");

            result.Should().BeNull();
        }

        // --- CreateAsync ---

        [Fact]
        public async Task CreateAsync_ValidUser_PersistsToDatabase()
        {
            var (service, context) = CreateService();
            var user = new User
            {
                Email = "newuser@test.com",
                PasswordHash = service.HashPassword("Password1")
            };

            var result = await service.CreateAsync(user);

            result.Id.Should().BeGreaterThan(0);
            context.Users.Count().Should().Be(1);
        }

        [Fact]
        public async Task CreateAsync_SetsDefaultValues()
        {
            var (service, _) = CreateService();
            var user = new User
            {
                Email = "new@test.com",
                PasswordHash = service.HashPassword("Pass123")
            };

            var result = await service.CreateAsync(user);

            result.IsAdmin.Should().BeFalse();
            result.BuddyGlasses.Should().BeNull();
            result.BuddyHat.Should().BeNull();
            result.BuddyMask.Should().BeNull();
        }

        [Fact]
        public async Task CreateAsync_AdminUser_PersistsAdminFlag()
        {
            var (service, context) = CreateService();
            var user = new User
            {
                Email = "admin@new.com",
                PasswordHash = service.HashPassword("Admin123"),
                IsAdmin = true
            };

            var result = await service.CreateAsync(user);

            result.IsAdmin.Should().BeTrue();
            context.Users.First().IsAdmin.Should().BeTrue();
        }

        // --- UpdateAsync ---

        [Fact]
        public async Task UpdateAsync_ChangeEmail_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var user = service.GetById(1)!;
            user.Email = "updated@test.com";

            await service.UpdateAsync(user);

            var updated = service.GetById(1);
            updated!.Email.Should().Be("updated@test.com");
        }

        [Fact]
        public async Task UpdateAsync_ChangeBuddyCustomization_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var user = service.GetById(1)!;
            user.BuddyGlasses = "round";
            user.BuddyHat = "top-hat";

            await service.UpdateAsync(user);

            var updated = service.GetById(1);
            updated!.BuddyGlasses.Should().Be("round");
            updated.BuddyHat.Should().Be("top-hat");
        }

        [Fact]
        public async Task UpdateAsync_PromoteToAdmin_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var user = service.GetById(1)!;
            user.IsAdmin.Should().BeFalse();
            user.IsAdmin = true;

            await service.UpdateAsync(user);

            service.GetById(1)!.IsAdmin.Should().BeTrue();
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_ExistingUser_RemovesFromDatabase()
        {
            var (service, context) = CreateServiceWithSeededData();

            await service.DeleteAsync(1);

            context.Users.Count().Should().Be(2);
            service.GetById(1).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_NonExistingId_DoesNotThrow()
        {
            var (service, context) = CreateServiceWithSeededData();

            var act = () => service.DeleteAsync(999);

            await act.Should().NotThrowAsync();
            context.Users.Count().Should().Be(3);
        }

        // --- EmailExists ---

        [Fact]
        public void EmailExists_ExistingEmail_ReturnsTrue()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.EmailExists("creator@test.com");

            result.Should().BeTrue();
        }

        [Fact]
        public void EmailExists_NonExistingEmail_ReturnsFalse()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.EmailExists("notreal@test.com");

            result.Should().BeFalse();
        }

        [Fact]
        public void EmailExists_EmptyDatabase_ReturnsFalse()
        {
            var (service, _) = CreateService();

            var result = service.EmailExists("any@test.com");

            result.Should().BeFalse();
        }

        [Fact]
        public void EmailExists_EmptyString_ReturnsFalse()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = service.EmailExists("");

            result.Should().BeFalse();
        }

        // --- HashPassword ---

        [Fact]
        public void HashPassword_SameInput_ProducesSameHash()
        {
            var (service, _) = CreateService();

            var hash1 = service.HashPassword("Password1");
            var hash2 = service.HashPassword("Password1");

            hash1.Should().Be(hash2);
        }

        [Fact]
        public void HashPassword_DifferentInputs_ProduceDifferentHashes()
        {
            var (service, _) = CreateService();

            var hash1 = service.HashPassword("Password1");
            var hash2 = service.HashPassword("Password2");

            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void HashPassword_ConsistentWithTestHelper()
        {
            var (service, _) = CreateService();

            var serviceHash = service.HashPassword("Password1");
            var helperHash = TestHelper.HashPassword("Password1");

            serviceHash.Should().Be(helperHash);
        }

        [Fact]
        public void HashPassword_NotPlainText()
        {
            var (service, _) = CreateService();

            var hash = service.HashPassword("MySecret");

            hash.Should().NotBe("MySecret");
            hash.Should().NotBeEmpty();
        }

        [Fact]
        public void HashPassword_EmptyString_ReturnsHash()
        {
            var (service, _) = CreateService();

            var hash = service.HashPassword("");

            hash.Should().NotBeEmpty();
        }
    }
}
