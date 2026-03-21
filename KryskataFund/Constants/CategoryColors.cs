namespace KryskataFund.Constants
{
    public static class CategoryColors
    {
        public static readonly Dictionary<string, string> Colors = new()
        {
            { "Education", "#4ade80" },
            { "Health", "#f97316" },
            { "Animals", "#22d3ee" },
            { "Creative", "#a855f7" },
            { "Dreams", "#facc15" },
            { "Just for fun", "#ef4444" },
            { "Technology", "#3b82f6" },
            { "Community", "#ec4899" }
        };

        public static string GetColor(string category, string defaultColor = "#4ade80")
        {
            return Colors.GetValueOrDefault(category, defaultColor);
        }
    }
}
