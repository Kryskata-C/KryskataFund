using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class FundCollaborator
    {
        public int Id { get; set; }

        [Required]
        public int FundId { get; set; }

        [Required]
        public int UserId { get; set; }

        public string Role { get; set; } = "collaborator";

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public Fund? Fund { get; set; }

        public User? User { get; set; }
    }
}
