using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using KryskataFund.Models;

namespace KryskataFund.Tests.Models
{
    public class ModelValidationTests
    {
        private static List<ValidationResult> ValidateModel(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model);
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }

        // --- CreateFundViewModel ---

        [Fact]
        public void CreateFundViewModel_ValidModel_PassesValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Fund Title",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 500,
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().BeEmpty();
        }

        [Fact]
        public void CreateFundViewModel_MissingTitle_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 500,
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Title"));
        }

        [Fact]
        public void CreateFundViewModel_TitleTooLong_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = new string('a', 201),
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 500,
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Title"));
        }

        [Fact]
        public void CreateFundViewModel_DescriptionTooShort_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Title",
                Description = "Short",
                Category = "Education",
                GoalAmount = 500,
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Description"));
        }

        [Fact]
        public void CreateFundViewModel_GoalTooLow_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Title",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 50, // Min is 100
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("GoalAmount"));
        }

        [Fact]
        public void CreateFundViewModel_GoalTooHigh_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Title",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 2000000, // Max is 1,000,000
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("GoalAmount"));
        }

        [Fact]
        public void CreateFundViewModel_DurationTooShort_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Title",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 500,
                DurationDays = 0
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("DurationDays"));
        }

        [Fact]
        public void CreateFundViewModel_DurationTooLong_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Title",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "Education",
                GoalAmount = 500,
                DurationDays = 91
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("DurationDays"));
        }

        [Fact]
        public void CreateFundViewModel_MissingCategory_FailsValidation()
        {
            var model = new CreateFundViewModel
            {
                Title = "Valid Title",
                Description = "This is a valid description that is at least fifty characters long for testing.",
                Category = "",
                GoalAmount = 500,
                DurationDays = 30
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Category"));
        }

        // --- SignUpViewModel ---

        [Fact]
        public void SignUpViewModel_ValidModel_PassesValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "Password1",
                ConfirmPassword = "Password1"
            };

            var results = ValidateModel(model);

            results.Should().BeEmpty();
        }

        [Fact]
        public void SignUpViewModel_MissingEmail_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "",
                Password = "Password1",
                ConfirmPassword = "Password1"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Email"));
        }

        [Fact]
        public void SignUpViewModel_InvalidEmail_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "notanemail",
                Password = "Password1",
                ConfirmPassword = "Password1"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Email"));
        }

        [Fact]
        public void SignUpViewModel_PasswordTooShort_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "Pass1",
                ConfirmPassword = "Pass1"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Password"));
        }

        [Fact]
        public void SignUpViewModel_PasswordMissingUppercase_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "password1",
                ConfirmPassword = "password1"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Password"));
        }

        [Fact]
        public void SignUpViewModel_PasswordMissingLowercase_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "PASSWORD1",
                ConfirmPassword = "PASSWORD1"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Password"));
        }

        [Fact]
        public void SignUpViewModel_PasswordMissingDigit_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "Password",
                ConfirmPassword = "Password"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Password"));
        }

        [Fact]
        public void SignUpViewModel_PasswordMismatch_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "Password1",
                ConfirmPassword = "Password2"
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("ConfirmPassword"));
        }

        [Fact]
        public void SignUpViewModel_MissingPassword_FailsValidation()
        {
            var model = new SignUpViewModel
            {
                Email = "user@example.com",
                Password = "",
                ConfirmPassword = ""
            };

            var results = ValidateModel(model);

            results.Should().Contain(r => r.MemberNames.Contains("Password"));
        }

        // --- Fund computed properties ---

        [Fact]
        public void Fund_DaysLeft_FutureEndDate_ReturnsPositive()
        {
            var fund = new Fund { EndDate = DateTime.UtcNow.AddDays(10) };

            fund.DaysLeft.Should().BeGreaterThanOrEqualTo(9);
            fund.DaysLeft.Should().BeLessThanOrEqualTo(10);
        }

        [Fact]
        public void Fund_DaysLeft_PastEndDate_ReturnsZero()
        {
            var fund = new Fund { EndDate = DateTime.UtcNow.AddDays(-5) };

            fund.DaysLeft.Should().Be(0);
        }

        [Fact]
        public void Fund_ProgressPercent_HalfFunded()
        {
            var fund = new Fund { GoalAmount = 1000, RaisedAmount = 500 };

            fund.ProgressPercent.Should().Be(50);
        }

        [Fact]
        public void Fund_ProgressPercent_FullyFunded()
        {
            var fund = new Fund { GoalAmount = 1000, RaisedAmount = 1000 };

            fund.ProgressPercent.Should().Be(100);
        }

        [Fact]
        public void Fund_ProgressPercent_OverFunded_CapsAt100()
        {
            var fund = new Fund { GoalAmount = 1000, RaisedAmount = 1500 };

            fund.ProgressPercent.Should().Be(100);
        }

        [Fact]
        public void Fund_ProgressPercent_ZeroGoal_ReturnsZero()
        {
            var fund = new Fund { GoalAmount = 0, RaisedAmount = 100 };

            fund.ProgressPercent.Should().Be(0);
        }

        [Fact]
        public void Fund_ProgressPercent_NoRaised_ReturnsZero()
        {
            var fund = new Fund { GoalAmount = 1000, RaisedAmount = 0 };

            fund.ProgressPercent.Should().Be(0);
        }

        // --- ErrorViewModel ---

        [Fact]
        public void ErrorViewModel_ShowRequestId_TrueWhenSet()
        {
            var model = new ErrorViewModel { RequestId = "abc123" };

            model.ShowRequestId.Should().BeTrue();
        }

        [Fact]
        public void ErrorViewModel_ShowRequestId_FalseWhenNull()
        {
            var model = new ErrorViewModel { RequestId = null };

            model.ShowRequestId.Should().BeFalse();
        }

        [Fact]
        public void ErrorViewModel_ShowRequestId_FalseWhenEmpty()
        {
            var model = new ErrorViewModel { RequestId = "" };

            model.ShowRequestId.Should().BeFalse();
        }
    }
}
