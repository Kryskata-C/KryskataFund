namespace KryskataFund.Models
{
    public class UserFollow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int FundId { get; set; }
        public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    }
}
