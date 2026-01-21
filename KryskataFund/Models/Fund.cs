using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KryskataFund.Models
{
    public class Fund
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public decimal GoalAmount { get; set; }

        public decimal RaisedAmount { get; set; } = 0;

        public int SupportersCount { get; set; } = 0;

        public int CreatorId { get; set; }

        public string CreatorName { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime EndDate { get; set; }

        public string CategoryColor { get; set; } = "#4ade80";

        [NotMapped]
        public int DaysLeft => Math.Max(0, (EndDate - DateTime.UtcNow).Days);

        [NotMapped]
        public int ProgressPercent => GoalAmount > 0
            ? (int)Math.Min(100, Math.Round(RaisedAmount / GoalAmount * 100))
            : 0;
    }
}
