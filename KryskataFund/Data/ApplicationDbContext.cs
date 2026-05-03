using Microsoft.EntityFrameworkCore;
using KryskataFund.Models;

namespace KryskataFund.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Fund> Funds { get; set; }
        public DbSet<Donation> Donations { get; set; }
        public DbSet<FundUpdate> FundUpdates { get; set; }
        public DbSet<UserFollow> UserFollows { get; set; }
        public DbSet<FundMilestone> FundMilestones { get; set; }
        public DbSet<DeadlineExtension> DeadlineExtensions { get; set; }
        public DbSet<FundCollaborator> FundCollaborators { get; set; }
        public DbSet<RecurringDonation> RecurringDonations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<FundComment> FundComments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<UserFollow>()
                .HasIndex(uf => new { uf.UserId, uf.FundId })
                .IsUnique();

            // SQL Server forbids multiple cascade paths from Users.
            // Sender/Receiver both reference Users — restrict delete on both.
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // FundComment: User -> Fund -> FundComment AND User -> FundComment
            // would create multiple cascade paths. Break the User side.
            modelBuilder.Entity<FundComment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Same issue for FundCollaborator.
            modelBuilder.Entity<FundCollaborator>()
                .HasOne(fc => fc.User)
                .WithMany()
                .HasForeignKey(fc => fc.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
