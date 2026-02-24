using git_heatmap_generator.Rendering;
using SixLabors.ImageSharp;

namespace git_heatmap_generator.Tests;

public class RendererTests
{
    [Theory]
    [InlineData(0, "#161b22")]
    [InlineData(1, "#0e4429")]
    [InlineData(3, "#0e4429")]
    [InlineData(4, "#006d32")]
    [InlineData(6, "#006d32")]
    [InlineData(7, "#26a641")]
    [InlineData(9, "#26a641")]
    [InlineData(10, "#39d353")]
    [InlineData(100, "#39d353")]
    public void GetColorForCount_ReturnsCorrectColor(int count, string expectedHex)
    {
        var color = HeatmapRenderer.GetColorForCount(count);
        Assert.Equal(Color.ParseHex(expectedHex), color);
    }

    [Theory]
    [InlineData(2024, 53)] // 2024 is a leap year starting on Monday
    [InlineData(2025, 53)] // 2025 starts on Wednesday
    public void CalculateWeeksForYear_ReturnsKnownValues(int year, int expectedWeeks)
    {
        var weeks = HeatmapRenderer.CalculateWeeksForYear(year);
        // Note: exact week count might vary by calculation method, but 53 is standard for a full year grid.
        Assert.Equal(expectedWeeks, weeks);
    }
}
