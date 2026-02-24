using git_heatmap_generator.Git;

namespace git_heatmap_generator.Tests;

public class CommitScannerTests
{
    [Fact]
    public void MergeCounts_CombinesCorrectly()
    {
        var dict1 = new Dictionary<DateTime, int>
        {
            { new DateTime(2025, 1, 1), 5 },
            { new DateTime(2025, 1, 2), 3 }
        };
        var dict2 = new Dictionary<DateTime, int>
        {
            { new DateTime(2025, 1, 2), 2 },
            { new DateTime(2025, 1, 3), 10 }
        };

        var result = CommitScanner.MergeCounts(dict1, dict2);

        Assert.Equal(3, result.Count);
        Assert.Equal(5, result[new DateTime(2025, 1, 1)]);
        Assert.Equal(5, result[new DateTime(2025, 1, 2)]); // 3 + 2
        Assert.Equal(10, result[new DateTime(2025, 1, 3)]);
    }
}
