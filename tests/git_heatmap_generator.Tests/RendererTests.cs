using git_heatmap_generator.Rendering;
using git_heatmap_generator.Models;
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

    [Fact]
    public void GetDefaultFileName_SingleYear_ReturnsCorrectName()
    {
        var years = new List<int> { 2025 };
        var fileName = HeatmapRenderer.GetDefaultFileName(years, HeatmapLayout.Vertical);
        Assert.Equal("heatmap_2025.png", fileName);
    }

    [Fact]
    public void GetDefaultFileName_YearRange_ReturnsRangeName()
    {
        var years = new List<int> { 2022, 2023, 2024 };
        var fileName = HeatmapRenderer.GetDefaultFileName(years, HeatmapLayout.Vertical);
        Assert.Equal("heatmap_2022-2024.png", fileName);
    }

    [Fact]
    public void GetDefaultFileName_HorizontalLayout_ReturnsHorizontalName()
    {
        var years = new List<int> { 2022, 2023 };
        var fileName = HeatmapRenderer.GetDefaultFileName(years, HeatmapLayout.Horizontal);
        Assert.Equal("heatmap_horizontal_2022-2023.png", fileName);
    }

    [Fact]
    public void GetDefaultFileName_SvgFormat_ReturnsCorrectExtension()
    {
        var years = new List<int> { 2025 };
        var fileName = HeatmapRenderer.GetDefaultFileName(years, HeatmapLayout.Vertical, OutputFormat.Svg);
        Assert.Equal("heatmap_2025.svg", fileName);
    }

    [Fact]
    public void GetDefaultFileName_SvgHorizontal_ReturnsCorrectName()
    {
        var years = new List<int> { 2022, 2023 };
        var fileName = HeatmapRenderer.GetDefaultFileName(years, HeatmapLayout.Horizontal, OutputFormat.Svg);
        Assert.Equal("heatmap_horizontal_2022-2023.svg", fileName);
    }
}


