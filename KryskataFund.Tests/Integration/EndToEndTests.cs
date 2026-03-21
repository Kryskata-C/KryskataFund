using FluentAssertions;
using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services;
using KryskataFund.Tests.Helpers;

namespace KryskataFund.Tests.Integration
{
    public class EndToEndTests
    {
        private ApplicationDbContext CreateFreshContext()
        {
            return TestHelper.CreateDbContext();
        }

        // --- Workflow 1: User signs up -> creates fund -> another user donates -> fund updated ---

        [Fact]
        public async Task FullWorkflow_SignUp_CreateFund_Donate_FundUpdated()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);
            var donationService = new DonationService(context);

            // Step 1: Creator signs up
            var creator = await userService.CreateAsync(new User
            {
                Email = "creator@flow.com",
                PasswordHash = TestHelper.HashPassword("Pass123")
            });
            creator.Id.Should().BeGreaterThan(0);

            // Step 2: Creator creates a fund
            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Save the Whales",
                Description = "Help protect marine life",
                Category = "Animals",
                GoalAmount = 5000,
                CreatorId = creator.Id,
                CreatorName = "@creator",
                EndDate = DateTime.UtcNow.AddDays(60)
            });
            fund.Id.Should().BeGreaterThan(0);

            // Step 3: Donor signs up
            var donor = await userService.CreateAsync(new User
            {
                Email = "donor@flow.com",
                PasswordHash = TestHelper.HashPassword("Pass456")
            });

            // Step 4: Donor makes a donation
            var donation = await donationService.CreateAsync(new Donation
            {
                FundId = fund.Id,
                UserId = donor.Id,
                DonorName = "@donor",
                Amount = 200
            });

            // Step 5: Update fund raised amount
            fund.RaisedAmount += donation.Amount;
            fund.SupportersCount += 1;
            await fundService.UpdateAsync(fund);

            // Verify
            var updatedFund = await fundService.GetByIdAsync(fund.Id);
            updatedFund!.RaisedAmount.Should().Be(200);
            updatedFund.SupportersCount.Should().Be(1);

            var donorDonations = (await donationService.GetByUserIdAsync(donor.Id)).ToList();
            donorDonations.Should().HaveCount(1);
            donorDonations.First().Amount.Should().Be(200);
        }

        [Fact]
        public async Task FullWorkflow_MultipleDonors_FundRaisedAccumulates()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);
            var donationService = new DonationService(context);

            var creator = await userService.CreateAsync(new User
            {
                Email = "owner@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Community Garden",
                Description = "Build a community garden",
                Category = "Community",
                GoalAmount = 3000,
                CreatorId = creator.Id,
                CreatorName = "@owner",
                EndDate = DateTime.UtcNow.AddDays(45)
            });

            // Three different donors
            for (int i = 1; i <= 3; i++)
            {
                var donor = await userService.CreateAsync(new User
                {
                    Email = $"donor{i}@test.com",
                    PasswordHash = TestHelper.HashPassword($"Pass{i}")
                });

                await donationService.CreateAsync(new Donation
                {
                    FundId = fund.Id,
                    UserId = donor.Id,
                    DonorName = $"@donor{i}",
                    Amount = 100 * i
                });

                fund.RaisedAmount += 100 * i;
                fund.SupportersCount += 1;
            }

            await fundService.UpdateAsync(fund);

            var updatedFund = await fundService.GetByIdAsync(fund.Id);
            updatedFund!.RaisedAmount.Should().Be(600); // 100 + 200 + 300
            updatedFund.SupportersCount.Should().Be(3);
        }

        // --- Workflow 2: Milestone tracking ---

        [Fact]
        public async Task FullWorkflow_FundMilestone_ReachedAfterDonation()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);
            var donationService = new DonationService(context);

            var creator = await userService.CreateAsync(new User
            {
                Email = "milestone_creator@test.com",
                PasswordHash = TestHelper.HashPassword("Pass123")
            });

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Milestone Fund",
                Description = "Testing milestones",
                Category = "Education",
                GoalAmount = 1000,
                CreatorId = creator.Id,
                CreatorName = "@creator",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            // Add milestone
            var milestone = new FundMilestone
            {
                FundId = fund.Id,
                Title = "First 500 raised",
                TargetAmount = 500,
                Description = "Halfway there!"
            };
            context.FundMilestones.Add(milestone);
            await context.SaveChangesAsync();

            // Donor donates enough to reach milestone
            var donor = await userService.CreateAsync(new User
            {
                Email = "big_donor@test.com",
                PasswordHash = TestHelper.HashPassword("Pass456")
            });

            await donationService.CreateAsync(new Donation
            {
                FundId = fund.Id,
                UserId = donor.Id,
                DonorName = "@bigdonor",
                Amount = 500
            });

            fund.RaisedAmount += 500;
            await fundService.UpdateAsync(fund);

            // Check milestone reached
            if (fund.RaisedAmount >= milestone.TargetAmount)
            {
                milestone.IsReached = true;
                milestone.ReachedAt = DateTime.UtcNow;
                context.FundMilestones.Update(milestone);
                await context.SaveChangesAsync();
            }

            var updatedMilestone = context.FundMilestones.First(m => m.Id == milestone.Id);
            updatedMilestone.IsReached.Should().BeTrue();
            updatedMilestone.ReachedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task FullWorkflow_MultipleMilestones_OnlyReachedOnesMarked()
        {
            var context = CreateFreshContext();
            var fundService = new FundService(context);
            var userService = new UserService(context);

            var creator = await userService.CreateAsync(new User
            {
                Email = "multi_mile@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Multi Milestone",
                Description = "Multiple milestones test",
                Category = "Health",
                GoalAmount = 2000,
                CreatorId = creator.Id,
                CreatorName = "@creator",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            context.FundMilestones.AddRange(
                new FundMilestone { FundId = fund.Id, Title = "25%", TargetAmount = 500 },
                new FundMilestone { FundId = fund.Id, Title = "50%", TargetAmount = 1000 },
                new FundMilestone { FundId = fund.Id, Title = "100%", TargetAmount = 2000 }
            );
            await context.SaveChangesAsync();

            // Fund reaches 750 raised
            fund.RaisedAmount = 750;
            await fundService.UpdateAsync(fund);

            var milestones = context.FundMilestones.Where(m => m.FundId == fund.Id).ToList();
            foreach (var m in milestones.Where(m => fund.RaisedAmount >= m.TargetAmount))
            {
                m.IsReached = true;
                m.ReachedAt = DateTime.UtcNow;
            }
            await context.SaveChangesAsync();

            var reached = context.FundMilestones.Where(m => m.FundId == fund.Id && m.IsReached).ToList();
            var notReached = context.FundMilestones.Where(m => m.FundId == fund.Id && !m.IsReached).ToList();

            reached.Should().HaveCount(1); // Only the 500 milestone
            notReached.Should().HaveCount(2);
        }

        // --- Workflow 3: Admin deletes user -> cascade effects ---

        [Fact]
        public async Task FullWorkflow_AdminDeletesUser_FundsAndDonationsRemoved()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);
            var donationService = new DonationService(context);

            // Create user with funds and donations
            var user = await userService.CreateAsync(new User
            {
                Email = "doomed@test.com",
                PasswordHash = TestHelper.HashPassword("Pass123")
            });

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Doomed Fund",
                Description = "Will be deleted",
                Category = "Other",
                GoalAmount = 1000,
                CreatorId = user.Id,
                CreatorName = "@doomed",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            await donationService.CreateAsync(new Donation
            {
                FundId = fund.Id,
                UserId = user.Id,
                DonorName = "@doomed",
                Amount = 50
            });

            // Verify data exists
            (await fundService.GetAllAsync()).Should().HaveCount(1);
            (await donationService.GetByUserIdAsync(user.Id)).Should().HaveCount(1);

            // Admin deletes user's donations and funds, then user
            var userDonations = (await donationService.GetByUserIdAsync(user.Id)).ToList();
            foreach (var d in userDonations)
            {
                await donationService.DeleteAsync(d.Id);
            }

            var userFunds = (await fundService.GetAllAsync()).Where(f => f.CreatorId == user.Id).ToList();
            foreach (var f in userFunds)
            {
                await fundService.DeleteAsync(f.Id);
            }

            await userService.DeleteAsync(user.Id);

            // Verify everything is cleaned up
            (await userService.GetByIdAsync(user.Id)).Should().BeNull();
            (await fundService.GetAllAsync()).Should().BeEmpty();
            (await donationService.GetByUserIdAsync(user.Id)).Should().BeEmpty();
        }

        [Fact]
        public async Task FullWorkflow_DeleteUser_OtherUsersUnaffected()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);

            var user1 = await userService.CreateAsync(new User
            {
                Email = "keep@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            var user2 = await userService.CreateAsync(new User
            {
                Email = "delete@test.com",
                PasswordHash = TestHelper.HashPassword("Pass2")
            });

            await userService.DeleteAsync(user2.Id);

            (await userService.GetByIdAsync(user1.Id)).Should().NotBeNull();
            (await userService.GetByIdAsync(user2.Id)).Should().BeNull();
        }

        // --- Workflow 4: User follows fund -> unfollows ---

        [Fact]
        public async Task FullWorkflow_UserFollowsFund_ThenUnfollows()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);

            var user = await userService.CreateAsync(new User
            {
                Email = "follower@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Popular Fund",
                Description = "Everyone follows this",
                Category = "Community",
                GoalAmount = 10000,
                CreatorId = 99,
                CreatorName = "@other",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            // Follow
            var follow = new UserFollow
            {
                UserId = user.Id,
                FundId = fund.Id
            };
            context.UserFollows.Add(follow);
            await context.SaveChangesAsync();

            context.UserFollows.Any(f => f.UserId == user.Id && f.FundId == fund.Id)
                .Should().BeTrue();

            // Unfollow
            var existingFollow = context.UserFollows
                .First(f => f.UserId == user.Id && f.FundId == fund.Id);
            context.UserFollows.Remove(existingFollow);
            await context.SaveChangesAsync();

            context.UserFollows.Any(f => f.UserId == user.Id && f.FundId == fund.Id)
                .Should().BeFalse();
        }

        [Fact]
        public async Task FullWorkflow_MultipleUsersFollowSameFund()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Viral Fund",
                Description = "Goes viral",
                Category = "Health",
                GoalAmount = 50000,
                CreatorId = 99,
                CreatorName = "@viral",
                EndDate = DateTime.UtcNow.AddDays(90)
            });

            for (int i = 1; i <= 5; i++)
            {
                var follower = await userService.CreateAsync(new User
                {
                    Email = $"follower{i}@test.com",
                    PasswordHash = TestHelper.HashPassword($"Pass{i}")
                });

                context.UserFollows.Add(new UserFollow
                {
                    UserId = follower.Id,
                    FundId = fund.Id
                });
            }
            await context.SaveChangesAsync();

            var followerCount = context.UserFollows.Count(f => f.FundId == fund.Id);
            followerCount.Should().Be(5);
        }

        [Fact]
        public async Task FullWorkflow_UserFollowsMultipleFunds()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);

            var user = await userService.CreateAsync(new User
            {
                Email = "multifollower@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            for (int i = 0; i < 3; i++)
            {
                var fund = await fundService.CreateAsync(new Fund
                {
                    Title = $"Fund {i}",
                    Description = "Desc",
                    Category = "Test",
                    GoalAmount = 1000,
                    CreatorId = 99,
                    CreatorName = "@other",
                    EndDate = DateTime.UtcNow.AddDays(30)
                });

                context.UserFollows.Add(new UserFollow
                {
                    UserId = user.Id,
                    FundId = fund.Id
                });
            }
            await context.SaveChangesAsync();

            var followedFunds = context.UserFollows.Count(f => f.UserId == user.Id);
            followedFunds.Should().Be(3);
        }

        // --- Workflow 5: Recurring donation creation -> cancellation ---

        [Fact]
        public async Task FullWorkflow_RecurringDonation_CreateAndCancel()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);

            var user = await userService.CreateAsync(new User
            {
                Email = "recurring@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Monthly Support",
                Description = "Recurring donations welcome",
                Category = "Education",
                GoalAmount = 12000,
                CreatorId = 99,
                CreatorName = "@org",
                EndDate = DateTime.UtcNow.AddDays(365)
            });

            // Create recurring donation
            var recurring = new RecurringDonation
            {
                FundId = fund.Id,
                UserId = user.Id,
                DonorName = "@recurring",
                Amount = 100,
                IsActive = true
            };
            context.RecurringDonations.Add(recurring);
            await context.SaveChangesAsync();

            recurring.Id.Should().BeGreaterThan(0);
            recurring.IsActive.Should().BeTrue();
            recurring.CancelledAt.Should().BeNull();

            // Cancel recurring donation
            recurring.IsActive = false;
            recurring.CancelledAt = DateTime.UtcNow;
            context.RecurringDonations.Update(recurring);
            await context.SaveChangesAsync();

            var cancelled = context.RecurringDonations.First(r => r.Id == recurring.Id);
            cancelled.IsActive.Should().BeFalse();
            cancelled.CancelledAt.Should().NotBeNull();
        }

        [Fact]
        public async Task FullWorkflow_MultipleRecurringDonations_CancelOne()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);
            var fundService = new FundService(context);

            var user = await userService.CreateAsync(new User
            {
                Email = "multirecur@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            var fund1 = await fundService.CreateAsync(new Fund
            {
                Title = "Fund A", Description = "A", Category = "Test",
                GoalAmount = 1000, CreatorId = 99, CreatorName = "@x",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            var fund2 = await fundService.CreateAsync(new Fund
            {
                Title = "Fund B", Description = "B", Category = "Test",
                GoalAmount = 2000, CreatorId = 99, CreatorName = "@x",
                EndDate = DateTime.UtcNow.AddDays(60)
            });

            var recurring1 = new RecurringDonation
            {
                FundId = fund1.Id, UserId = user.Id,
                DonorName = "@user", Amount = 50, IsActive = true
            };
            var recurring2 = new RecurringDonation
            {
                FundId = fund2.Id, UserId = user.Id,
                DonorName = "@user", Amount = 75, IsActive = true
            };
            context.RecurringDonations.AddRange(recurring1, recurring2);
            await context.SaveChangesAsync();

            // Cancel only the first one
            recurring1.IsActive = false;
            recurring1.CancelledAt = DateTime.UtcNow;
            context.RecurringDonations.Update(recurring1);
            await context.SaveChangesAsync();

            var active = context.RecurringDonations.Where(r => r.UserId == user.Id && r.IsActive).ToList();
            var cancelled = context.RecurringDonations.Where(r => r.UserId == user.Id && !r.IsActive).ToList();

            active.Should().HaveCount(1);
            active.First().FundId.Should().Be(fund2.Id);
            cancelled.Should().HaveCount(1);
        }

        // --- Additional integration scenarios ---

        [Fact]
        public async Task Integration_SearchFindsNewlyCreatedFund()
        {
            var context = CreateFreshContext();
            var fundService = new FundService(context);

            await fundService.CreateAsync(new Fund
            {
                Title = "Unique Butterfly Conservation",
                Description = "Saving butterflies",
                Category = "Animals",
                GoalAmount = 2000,
                CreatorId = 1,
                CreatorName = "@conserve",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            var results = (await fundService.SearchAsync("Butterfly")).ToList();

            results.Should().HaveCount(1);
            results.First().Title.Should().Contain("Butterfly");
        }

        [Fact]
        public async Task Integration_TotalRaisedUpdatesAfterDonation()
        {
            var context = CreateFreshContext();
            var fundService = new FundService(context);
            var donationService = new DonationService(context);

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Total Test",
                Description = "Testing totals",
                Category = "Test",
                GoalAmount = 5000,
                CreatorId = 1,
                CreatorName = "@test",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            (await fundService.GetTotalRaisedAsync()).Should().Be(0);

            fund.RaisedAmount = 1500;
            await fundService.UpdateAsync(fund);

            (await fundService.GetTotalRaisedAsync()).Should().Be(1500);
        }

        [Fact]
        public async Task Integration_ActiveCampaignCount_ExcludesEndedFunds()
        {
            var context = CreateFreshContext();
            var fundService = new FundService(context);

            await fundService.CreateAsync(new Fund
            {
                Title = "Active", Description = "Still going", Category = "Test",
                GoalAmount = 1000, CreatorId = 1, CreatorName = "@test",
                EndDate = DateTime.UtcNow.AddDays(30)
            });

            await fundService.CreateAsync(new Fund
            {
                Title = "Ended", Description = "Already done", Category = "Test",
                GoalAmount = 1000, CreatorId = 1, CreatorName = "@test",
                EndDate = DateTime.UtcNow.AddDays(-5)
            });

            (await fundService.GetActiveCampaignCountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Integration_DeleteFund_DonationsOrphaned()
        {
            var context = CreateFreshContext();
            var fundService = new FundService(context);
            var donationService = new DonationService(context);

            var fund = await fundService.CreateAsync(new Fund
            {
                Title = "Temporary", Description = "Will be removed", Category = "Test",
                GoalAmount = 1000, CreatorId = 1, CreatorName = "@test",
                EndDate = DateTime.UtcNow.AddDays(10)
            });

            await donationService.CreateAsync(new Donation
            {
                FundId = fund.Id, UserId = 1, DonorName = "@user", Amount = 100
            });

            (await donationService.GetByFundIdAsync(fund.Id)).Should().HaveCount(1);

            await fundService.DeleteAsync(fund.Id);

            (await fundService.GetByIdAsync(fund.Id)).Should().BeNull();
        }

        [Fact]
        public async Task Integration_GetTopFunded_ReflectsLatestUpdates()
        {
            var context = CreateFreshContext();
            var fundService = new FundService(context);

            var fundA = await fundService.CreateAsync(new Fund
            {
                Title = "Fund A", Description = "A", Category = "Test",
                GoalAmount = 1000, RaisedAmount = 100, CreatorId = 1,
                CreatorName = "@test", EndDate = DateTime.UtcNow.AddDays(30)
            });

            var fundB = await fundService.CreateAsync(new Fund
            {
                Title = "Fund B", Description = "B", Category = "Test",
                GoalAmount = 2000, RaisedAmount = 50, CreatorId = 1,
                CreatorName = "@test", EndDate = DateTime.UtcNow.AddDays(30)
            });

            var top = (await fundService.GetTopFundedAsync(1)).First();
            top.Title.Should().Be("Fund A");

            // Fund B gets a big donation
            fundB.RaisedAmount = 500;
            await fundService.UpdateAsync(fundB);

            top = (await fundService.GetTopFundedAsync(1)).First();
            top.Title.Should().Be("Fund B");
        }

        [Fact]
        public async Task Integration_UserEmailUniqueness_ServiceCheck()
        {
            var context = CreateFreshContext();
            var userService = new UserService(context);

            await userService.CreateAsync(new User
            {
                Email = "unique@test.com",
                PasswordHash = TestHelper.HashPassword("Pass1")
            });

            (await userService.EmailExistsAsync("unique@test.com")).Should().BeTrue();
            (await userService.EmailExistsAsync("other@test.com")).Should().BeFalse();
        }
    }
}
