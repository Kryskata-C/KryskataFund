using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class RecurringDonation
    {
        public int Id { get; set; }

        public int FundId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string DonorName { get; set; } = "Anonymous";

        [Required]
        [Range(0.01, (double)decimal.MaxValue)]
        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public DateTime? CancelledAt { get; set; }

        // Navigation property
        public Fund? Fund { get; set; }
    }
}
