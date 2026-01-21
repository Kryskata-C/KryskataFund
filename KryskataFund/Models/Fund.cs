namespace KryskataFund.Models
{
    public class Fund
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal GoalAmount { get; set; }
        public decimal RaisedAmount { get; set; }
        public int SupportersCount { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int DaysLeft { get; set; }
        public string CategoryColor { get; set; } = "#4ade80";

        public int ProgressPercent => GoalAmount > 0
            ? (int)Math.Min(100, Math.Round(RaisedAmount / GoalAmount * 100))
            : 0;
    }
}
