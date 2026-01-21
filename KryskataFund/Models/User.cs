using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Buddy customization
        public string? BuddyGlasses { get; set; }
        public string? BuddyHat { get; set; }
        public string? BuddyMask { get; set; }
    }
}
