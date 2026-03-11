using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class DeadlineExtension
    {
        public int Id { get; set; }

        public int FundId { get; set; }

        public DateTime OriginalEndDate { get; set; }

        public DateTime NewEndDate { get; set; }

        public int ExtensionDays { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Fund? Fund { get; set; }
    }
}
