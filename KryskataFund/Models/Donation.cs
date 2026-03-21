using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class Donation
    {
        public int Id { get; set; }

        public int FundId { get; set; }

        public int? UserId { get; set; }

        [Required]
        public string DonorName { get; set; } = "Anonymous";

        [Required]
        [Range(0.01, (double)decimal.MaxValue)]
        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Fund? Fund { get; set; }
        public virtual User? User { get; set; }
    }
}
