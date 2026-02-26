using git_heatmap_generator.Cli;
using git_heatmap_generator.Models;

namespace git_heatmap_generator.Tests;

public class ArgumentParserTests
{
    [Fact]
    public void ParseYears_SingleYear_ReturnsListWithOneYear()
    {
        var result = ArgumentParser.ParseYears("2025");
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(2025, result[0]);
    }

    [Fact]
    public void ParseYears_Range_ReturnsListWithMultipleYears()
    {
        var result = ArgumentParser.ParseYears("2022...2024");
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(2022, result);
        Assert.Contains(2023, result);
        Assert.Contains(2024, result);
    }

    [Fact]
    public void Parse_ValidArgs_ReturnsParsedArguments()
    {
        string[] args = { "2025", "user@example.com", "/path/to/repo" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Contains(2025, result.Years);
        Assert.Contains("user@example.com", result.Emails);
        Assert.Contains("/path/to/repo", result.RepositoryPaths);
        Assert.Equal(HeatmapLayout.Vertical, result.Layout);
    }

    [Fact]
    public void Parse_HorizontalLayout_ReturnsLayoutHorizontal()
    {
        string[] args = { "2025", "user@example.com", "/path/to/repo", "-l", "horizontal" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(HeatmapLayout.Horizontal, result.Layout);
    }

    [Fact]
    public void Parse_WithYearAndEmailFlags_ReturnsParsedArguments()
    {
        string[] args = { "--year", "2024", "--email", "test@mail.com", "/path" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Contains(2024, result.Years);
        Assert.Contains("test@mail.com", result.Emails);
        Assert.Contains("/path", result.RepositoryPaths);
    }

    [Fact]
    public void Parse_WithMultipleYearAndEmailFlags_AggregatesResults()
    {
        string[] args = { "-y", "2024", "-y", "2025", "-e", "a@b.com", "-e", "c@d.com", "/path" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(2, result.Years.Count);
        Assert.Contains(2024, result.Years);
        Assert.Contains(2025, result.Years);
        Assert.Equal(2, result.Emails.Count);
        Assert.Contains("a@b.com", result.Emails);
        Assert.Contains("c@d.com", result.Emails);
    }

    [Fact]
    public void Parse_WithRepoFlag_ReturnsParsedArguments()
    {
        string[] args = { "--repo", "/custom/path", "2025", "user@mail.com" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Contains("/custom/path", result.RepositoryPaths);
        Assert.Contains(2025, result.Years);
        Assert.Contains("user@mail.com", result.Emails);
    }

    [Fact]
    public void Parse_WithMultipleRepos_AggregatesRepos()
    {
        string[] args = { "2025", "user@mail.com", "/repo1", "/repo2", "-r", "/repo3" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(3, result.RepositoryPaths.Count);
        Assert.Contains("/repo1", result.RepositoryPaths);
        Assert.Contains("/repo2", result.RepositoryPaths);
        Assert.Contains("/repo3", result.RepositoryPaths);
    }

    [Fact]
    public void Parse_MissingPath_ReturnsNull()
    {
        string[] args = { "-y", "2025", "-e", "user@mail.com" };
        var result = ArgumentParser.Parse(args);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_WithPullRequestsFlag_ReturnsIncludePullRequestsTrue()
    {
        string[] args = { "2025", "user@mail.com", "/path", "--pull-requests" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.True(result.IncludePullRequests);
    }

    [Fact]
    public void Parse_WithShortPrFlag_ReturnsIncludePullRequestsTrue()
    {
        string[] args = { "2025", "user@mail.com", "/path", "-pr" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.True(result.IncludePullRequests);
    }

    [Fact]
    public void Parse_WithSmartDash_NormalizesAndParses()
    {
        // Using em-dash (—) which macOS often auto-corrects to
        string[] args = { "—year", "2025", "—email", "user@mail.com", "—repo", "/path" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Contains(2025, result.Years);
        Assert.Contains("user@mail.com", result.Emails);
        Assert.Contains("/path", result.RepositoryPaths);
    }

    [Fact]
    public void ParseYears_WithUnicodeEllipsis_ReturnsRange()
    {
        var result = ArgumentParser.ParseYears("2022\u20262024");
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains(2022, result);
        Assert.Contains(2023, result);
        Assert.Contains(2024, result);
    }

    [Fact]
    public void Parse_LayoutSeparate_ReturnsLayoutSeparate()
    {
        string[] args = { "2025", "user@mail.com", "/path", "--layout", "separate" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(HeatmapLayout.Separate, result.Layout);
    }

    [Fact]
    public void Parse_HelpFlag_ReturnsShowHelpTrue()
    {
        string[] args = { "--help" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.True(result.ShowHelp);
    }

    [Fact]
    public void ParseEmails_WithWhitespace_CleansAndFilters()
    {
        var result = ArgumentParser.ParseEmails(" a@b.com, ,  c@d.com ");
        Assert.Equal(2, result.Count);
        Assert.Equal("a@b.com", result[0]);
        Assert.Equal("c@d.com", result[1]);
    }

    [Fact]
    public void ParseYears_ReversedRange_ReturnsYearsInOrder()
    {
        var result = ArgumentParser.ParseYears("2024...2022");
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(2022, result[0]);
        Assert.Equal(2023, result[1]);
        Assert.Equal(2024, result[2]);
    }

    [Fact]
    public void Parse_DuplicateArguments_ReturnsDistinctLists()
    {
        string[] args = { "2025", "user@mail.com", "/path", "-y", "2025", "-e", "USER@mail.com" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Single(result.Years);
        Assert.Single(result.Emails);
    }

    [Fact]
    public void Parse_WithFormatFlagSvg_ReturnsOutputFormatSvg()
    {
        string[] args = { "2025", "user@mail.com", "/path", "--format", "svg" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(OutputFormat.Svg, result.Format);
    }

    [Fact]
    public void Parse_WithSvgExtension_InfersOutputFormatSvg()
    {
        string[] args = { "2025", "user@mail.com", "/path", "-o", "result.svg" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(OutputFormat.Svg, result.Format);
    }

    [Fact]
    public void Parse_WithPngExtension_InfersOutputFormatPng()
    {
        string[] args = { "2025", "user@mail.com", "/path", "-o", "result.png" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(OutputFormat.Png, result.Format);
    }

    [Fact]
    public void Parse_WithStyleFlag_ReturnsColorTheme()
    {
        string[] args = { "2025", "user@mail.com", "/path", "--style", "red" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(ColorTheme.Red, result.Theme);
    }

    [Fact]
    public void Parse_WithShortStyleFlag_ReturnsColorTheme()
    {
        string[] args = { "2025", "user@mail.com", "/path", "-s", "blue" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(ColorTheme.Blue, result.Theme);
    }

    [Fact]
    public void Parse_WithModeFlag_ReturnsColorMode()
    {
        string[] args = { "2025", "user@mail.com", "/path", "--mode", "light" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(ColorMode.Light, result.Mode);
    }

    [Fact]
    public void Parse_WithShortModeFlag_ReturnsColorMode()
    {
        string[] args = { "2025", "user@mail.com", "/path", "-m", "dark" };
        var result = ArgumentParser.Parse(args);
        Assert.NotNull(result);
        Assert.Equal(ColorMode.Dark, result.Mode);
    }
}




