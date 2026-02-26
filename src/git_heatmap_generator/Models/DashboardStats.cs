namespace git_heatmap_generator.Models;

public class DashboardStats
{
    public int TotalCommits { get; set; }
    public int ActiveDays { get; set; }
    public int MaxCommitsPerDay { get; set; }
    public int LongestStreak { get; set; }
    public double AverageCommitsPerActiveDay { get; set; }
    public DayOfWeek MostActiveDayOfWeek { get; set; }
    public string MostActiveMonth { get; set; } = string.Empty;
    public DateTime? FirstActivity { get; set; }
    public DateTime? LastActivity { get; set; }

    public static DashboardStats Calculate(Dictionary<DateTime, int> commitCounts, List<int> years)
    {
        var stats = new DashboardStats();
        if (commitCounts.Count == 0) return stats;

        var relevantCounts = commitCounts
            .Where(kv => years.Contains(kv.Key.Year))
            .OrderBy(kv => kv.Key)
            .ToList();

        if (relevantCounts.Count == 0) return stats;

        stats.TotalCommits = relevantCounts.Sum(kv => kv.Value);
        stats.ActiveDays = relevantCounts.Count;
        stats.MaxCommitsPerDay = relevantCounts.Max(kv => kv.Value);
        stats.AverageCommitsPerActiveDay = (double)stats.TotalCommits / stats.ActiveDays;
        stats.FirstActivity = relevantCounts.First().Key;
        stats.LastActivity = relevantCounts.Last().Key;

        stats.MostActiveDayOfWeek = relevantCounts
            .GroupBy(kv => kv.Key.DayOfWeek)
            .OrderByDescending(g => g.Sum(kv => kv.Value))
            .First().Key;

        stats.MostActiveMonth = relevantCounts
            .GroupBy(kv => kv.Key.Month)
            .OrderByDescending(g => g.Sum(kv => kv.Value))
            .First().Key switch
            {
                1 => "January",
                2 => "February",
                3 => "March",
                4 => "April",
                5 => "May",
                6 => "June",
                7 => "July",
                8 => "August",
                9 => "September",
                10 => "October",
                11 => "November",
                12 => "December",
                _ => "Unknown"
            };

        // Calculate longest streak
        int longest = 0;
        int current = 0;
        DateTime? prevDate = null;

        foreach (var kvp in relevantCounts)
        {
            if (prevDate == null)
            {
                current = 1;
            }
            else if (kvp.Key == prevDate.Value.AddDays(1))
            {
                current++;
            }
            else
            {
                if (current > longest) longest = current;
                current = 1;
            }
            prevDate = kvp.Key;
        }
        if (current > longest) longest = current;

        stats.LongestStreak = longest;

        return stats;
    }
}
