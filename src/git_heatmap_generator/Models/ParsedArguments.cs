namespace git_heatmap_generator.Models;

public enum HeatmapLayout { Vertical, Horizontal, Separate }
public enum OutputFormat { Png, Svg }

/// <summary>
/// Result of parsing command-line arguments.
/// </summary>
public class ParsedArguments
{
    public List<int> Years { get; set; } = new();
    public List<string> Emails { get; set; } = new();
    public List<string> RepositoryPaths { get; set; } = new();
    public string OutputFolder { get; set; } = ".";
    public HeatmapLayout Layout { get; set; } = HeatmapLayout.Vertical;
    public bool ShowHelp { get; set; }
    public bool IncludePullRequests { get; set; }
    public OutputFormat Format { get; set; } = OutputFormat.Png;
    public ColorTheme Theme { get; set; } = ColorTheme.Default;
    public ColorMode Mode { get; set; } = ColorMode.Dark;
}


