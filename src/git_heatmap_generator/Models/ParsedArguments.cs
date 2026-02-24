namespace git_heatmap_generator.Models;

public enum HeatmapLayout { Vertical, Horizontal, Separate }

/// <summary>
/// Result of parsing command-line arguments.
/// </summary>
public class ParsedArguments
{
    public List<int> Years { get; set; } = new();
    public List<string> Emails { get; set; } = new();
    public string RepositoryPath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = ".";
    public HeatmapLayout Layout { get; set; } = HeatmapLayout.Vertical;
    public bool ShowHelp { get; set; }
    public bool IncludePullRequests { get; set; }
}

