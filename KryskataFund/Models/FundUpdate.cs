using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class FundUpdate
    {
        public int Id { get; set; }

        [Required]
        public int FundId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Fund? Fund { get; set; }
    }
}
