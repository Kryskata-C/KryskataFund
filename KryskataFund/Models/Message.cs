using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [Required]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        public string ReceiverName { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public int? SharedFundId { get; set; }

        // Navigation properties
        public virtual User? Sender { get; set; }
        public virtual User? Receiver { get; set; }
    }
}
