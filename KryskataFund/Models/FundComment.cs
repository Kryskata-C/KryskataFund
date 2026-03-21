using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class FundComment
    {
        public int Id { get; set; }

        public int FundId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual Fund? Fund { get; set; }
    }
}
