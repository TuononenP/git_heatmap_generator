using git_heatmap_generator.Models;

namespace git_heatmap_generator.Tests;

public class DashboardStatsTests
{
    [Fact]
    public void Calculate_WithNoData_ReturnsEmptyStats()
    {
        var stats = DashboardStats.Calculate(new Dictionary<DateTime, int>(), new List<int> { 2025 });
        Assert.Equal(0, stats.TotalCommits);
        Assert.Equal(0, stats.ActiveDays);
    }

    [Fact]
    public void Calculate_WithData_ReturnsCorrectStats()
    {
        var data = new Dictionary<DateTime, int>
        {
            { new DateTime(2025, 1, 1), 5 },
            { new DateTime(2025, 1, 2), 3 },
            { new DateTime(2025, 1, 4), 10 }
        };
        var stats = DashboardStats.Calculate(data, new List<int> { 2025 });
        
        Assert.Equal(18, stats.TotalCommits);
        Assert.Equal(3, stats.ActiveDays);
        Assert.Equal(10, stats.MaxCommitsPerDay);
        Assert.Equal(6.0, stats.AverageCommitsPerActiveDay);
        Assert.Equal(2, stats.LongestStreak); // 1, 2
        Assert.Equal(DayOfWeek.Saturday, stats.MostActiveDayOfWeek);
        Assert.Equal("January", stats.MostActiveMonth);
    }

    [Fact]
    public void Calculate_StreakAcrossYears()
    {
         var data = new Dictionary<DateTime, int>
        {
            { new DateTime(2024, 12, 31), 1 },
            { new DateTime(2025, 1, 1), 1 },
            { new DateTime(2025, 1, 2), 1 }
        };
        var stats = DashboardStats.Calculate(data, new List<int> { 2024, 2025 });
        
        Assert.Equal(3, stats.LongestStreak);
    }
}
