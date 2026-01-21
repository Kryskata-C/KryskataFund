using System.ComponentModel.DataAnnotations;

namespace KryskataFund.Models
{
    public class CreateFundViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [MinLength(50, ErrorMessage = "Description must be at least 50 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Goal amount is required")]
        [Range(100, 1000000, ErrorMessage = "Goal must be between €100 and €1,000,000")]
        public decimal GoalAmount { get; set; }

        [Required(ErrorMessage = "Campaign duration is required")]
        [Range(1, 90, ErrorMessage = "Campaign must run between 1 and 90 days")]
        public int DurationDays { get; set; } = 30;

        public string? ImageUrl { get; set; }

        public IFormFile? ImageFile { get; set; }
    }
}
