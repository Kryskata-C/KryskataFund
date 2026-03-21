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

        // --- GetByIdAsync ---

        [Fact]
        public async Task GetById_ExistingUser_ReturnsUser()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByIdAsync(1);

            result.Should().NotBeNull();
            result!.Email.Should().Be("creator@test.com");
        }

        [Fact]
        public async Task GetById_NonExistingUser_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByIdAsync(999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetById_AdminUser_HasIsAdminTrue()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByIdAsync(3);

            result.Should().NotBeNull();
            result!.IsAdmin.Should().BeTrue();
        }

        // --- GetByEmailAsync ---

        [Fact]
        public async Task GetByEmail_ExistingEmail_ReturnsUser()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByEmailAsync("creator@test.com");

            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
        }

        [Fact]
        public async Task GetByEmail_NonExistingEmail_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByEmailAsync("nonexistent@test.com");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByEmail_EmptyString_ReturnsNull()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.GetByEmailAsync("");

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
                PasswordHash = TestHelper.HashPassword("Password1")
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
                PasswordHash = TestHelper.HashPassword("Pass123")
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
                PasswordHash = TestHelper.HashPassword("Admin123"),
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
            var user = (await service.GetByIdAsync(1))!;
            user.Email = "updated@test.com";

            await service.UpdateAsync(user);

            var updated = await service.GetByIdAsync(1);
            updated!.Email.Should().Be("updated@test.com");
        }

        [Fact]
        public async Task UpdateAsync_ChangeBuddyCustomization_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var user = (await service.GetByIdAsync(1))!;
            user.BuddyGlasses = "round";
            user.BuddyHat = "top-hat";

            await service.UpdateAsync(user);

            var updated = await service.GetByIdAsync(1);
            updated!.BuddyGlasses.Should().Be("round");
            updated.BuddyHat.Should().Be("top-hat");
        }

        [Fact]
        public async Task UpdateAsync_PromoteToAdmin_Persists()
        {
            var (service, _) = CreateServiceWithSeededData();
            var user = (await service.GetByIdAsync(1))!;
            user.IsAdmin.Should().BeFalse();
            user.IsAdmin = true;

            await service.UpdateAsync(user);

            (await service.GetByIdAsync(1))!.IsAdmin.Should().BeTrue();
        }

        // --- DeleteAsync ---

        [Fact]
        public async Task DeleteAsync_ExistingUser_RemovesFromDatabase()
        {
            var (service, context) = CreateServiceWithSeededData();

            await service.DeleteAsync(1);

            context.Users.Count().Should().Be(2);
            (await service.GetByIdAsync(1)).Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_NonExistingId_DoesNotThrow()
        {
            var (service, context) = CreateServiceWithSeededData();

            var act = () => service.DeleteAsync(999);

            await act.Should().NotThrowAsync();
            context.Users.Count().Should().Be(3);
        }

        // --- EmailExistsAsync ---

        [Fact]
        public async Task EmailExists_ExistingEmail_ReturnsTrue()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.EmailExistsAsync("creator@test.com");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task EmailExists_NonExistingEmail_ReturnsFalse()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.EmailExistsAsync("notreal@test.com");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task EmailExists_EmptyDatabase_ReturnsFalse()
        {
            var (service, _) = CreateService();

            var result = await service.EmailExistsAsync("any@test.com");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task EmailExists_EmptyString_ReturnsFalse()
        {
            var (service, _) = CreateServiceWithSeededData();

            var result = await service.EmailExistsAsync("");

            result.Should().BeFalse();
        }

        // --- HashPassword (PasswordHasher) ---

        [Fact]
        public void HashPassword_ProducesVerifiableHash()
        {
            var hash = TestHelper.HashPassword("Password1");

            PasswordHasher.VerifyPassword("Password1", hash).Should().BeTrue();
        }

        [Fact]
        public void HashPassword_DifferentInputs_ProduceDifferentHashes()
        {
            var hash1 = TestHelper.HashPassword("Password1");
            var hash2 = TestHelper.HashPassword("Password2");

            PasswordHasher.VerifyPassword("Password1", hash2).Should().BeFalse();
            PasswordHasher.VerifyPassword("Password2", hash1).Should().BeFalse();
        }

        [Fact]
        public void HashPassword_ConsistentWithTestHelper()
        {
            // Both TestHelper and PasswordHasher now use BCrypt.
            // BCrypt hashes are non-deterministic (different salt each time),
            // so verify that PasswordHasher.VerifyPassword accepts TestHelper hashes.
            var helperHash = TestHelper.HashPassword("Password1");

            KryskataFund.Services.PasswordHasher.VerifyPassword("Password1", helperHash).Should().BeTrue();
        }

        [Fact]
        public void HashPassword_NotPlainText()
        {
            var hash = TestHelper.HashPassword("MySecret");

            hash.Should().NotBe("MySecret");
            hash.Should().NotBeEmpty();
        }

        [Fact]
        public void HashPassword_EmptyString_ReturnsHash()
        {
            var hash = TestHelper.HashPassword("");

            hash.Should().NotBeEmpty();
        }
    }
}
