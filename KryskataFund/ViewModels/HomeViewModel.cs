using KryskataFund.Models;

namespace KryskataFund.ViewModels
{
    public class HomeViewModel
    {
        public List<Fund> Funds { get; set; } = new();
        public Dictionary<string, int> CategoryCounts { get; set; } = new();
        public int TotalCount { get; set; }
        public string? SelectedCategory { get; set; }
        public decimal TotalRaised { get; set; }
        public int LiveCampaigns { get; set; }
        public decimal TodaysImpact { get; set; }
        public decimal AvgSupport { get; set; }
        public string TopCategory { get; set; } = "None";
        public List<int> FollowedFundIds { get; set; } = new();
        public List<int> ExpiredFundIds { get; set; } = new();
    }
}
