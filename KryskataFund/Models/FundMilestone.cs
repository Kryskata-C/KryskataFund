using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class FundMilestone
    {
        public int Id { get; set; }

        [Required]
        public int FundId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Range(1, (double)decimal.MaxValue)]
        public decimal TargetAmount { get; set; }

        public string? Description { get; set; }

        public bool IsReached { get; set; } = false;

        public DateTime? ReachedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Fund? Fund { get; set; }
    }
}
