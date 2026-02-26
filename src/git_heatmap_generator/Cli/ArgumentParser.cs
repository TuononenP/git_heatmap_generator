using git_heatmap_generator.Models;

namespace git_heatmap_generator.Cli;

/// <summary>
/// Parses and validates command-line arguments.
/// </summary>
public static class ArgumentParser
{
    private static readonly string[] HelpFlags = { "--help", "-h", "—help" };
    private static readonly string[] OutputFlags = { "--output", "-o", "—output" };
    private static readonly string[] LayoutFlags = { "--layout", "-l", "—layout" };
    private static readonly string[] YearFlags = { "--year", "-y", "—year" };
    private static readonly string[] EmailFlags = { "--email", "-e", "—email" };
    private static readonly string[] RepoFlags = { "--repo", "-r", "—repo" };
    private static readonly string[] PrFlags = { "--pull-requests", "-pr", "—pull-requests" };
    private static readonly string[] FormatFlags = { "--format", "-f", "—format" };

    /// <summary>
    /// Parses command-line arguments into a structured result.
    /// Returns null if arguments are insufficient or help was requested.
    /// </summary>
    public static ParsedArguments? Parse(string[] args)
    {
        var result = new ParsedArguments();
        var positionalArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            
            // Normalize smart dashes (em-dash) for flags
            if (arg.StartsWith("—"))
            {
                arg = "--" + arg.Substring(1);
            }

            if (HelpFlags.Contains(arg))
            {
                result.ShowHelp = true;
            }
            else if (OutputFlags.Contains(arg) && i + 1 < args.Length)
            {
                result.OutputFolder = args[++i];
            }
            else if (LayoutFlags.Contains(arg) && i + 1 < args.Length)
            {
                string layoutArg = args[++i].ToLower();
                result.Layout = layoutArg switch
                {
                    "horizontal" => HeatmapLayout.Horizontal,
                    "separate" => HeatmapLayout.Separate,
                    _ => HeatmapLayout.Vertical
                };
            }
            else if (YearFlags.Contains(arg) && i + 1 < args.Length)
            {
                var parsedYears = ParseYears(args[++i]);
                if (parsedYears != null) result.Years.AddRange(parsedYears);
            }
            else if (EmailFlags.Contains(arg) && i + 1 < args.Length)
            {
                var parsedEmails = ParseEmails(args[++i]);
                result.Emails.AddRange(parsedEmails);
            }
            else if (RepoFlags.Contains(arg) && i + 1 < args.Length)
            {
                result.RepositoryPath = args[++i];
            }
            else if (PrFlags.Contains(arg))
            {
                result.IncludePullRequests = true;
            }
            else if (FormatFlags.Contains(arg) && i + 1 < args.Length)
            {
                string formatArg = args[++i].ToLower();
                result.Format = formatArg == "svg" ? OutputFormat.Svg : OutputFormat.Png;
            }
            else
            {
                positionalArgs.Add(args[i]);
            }
        }

        if (result.ShowHelp) return result;

        // Fallback to positional arguments if flags weren't used for years/emails/path
        int posIndex = 0;
        if (result.Years.Count == 0 && posIndex < positionalArgs.Count)
        {
            var parsedYears = ParseYears(positionalArgs[posIndex]);
            if (parsedYears != null)
            {
                result.Years.AddRange(parsedYears);
                posIndex++;
            }
        }

        if (result.Emails.Count == 0 && posIndex < positionalArgs.Count)
        {
            var parsedEmails = ParseEmails(positionalArgs[posIndex]);
            result.Emails.AddRange(parsedEmails);
            posIndex++;
        }

        if (string.IsNullOrEmpty(result.RepositoryPath) && posIndex < positionalArgs.Count)
        {
            result.RepositoryPath = positionalArgs[posIndex];
            posIndex++;
        }

        // Validation
        if (result.Years.Count == 0 || result.Emails.Count == 0 || string.IsNullOrEmpty(result.RepositoryPath))
        {
            return null;
        }

        // Distinct lists
        result.Years = result.Years.Distinct().OrderBy(y => y).ToList();
        result.Emails = result.Emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Infer format from output path if possible
        if (result.OutputFolder.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            result.Format = OutputFormat.Svg;
        else if (result.OutputFolder.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            result.Format = OutputFormat.Png;

        return result;
    }

    /// <summary>
    /// Parses a year string into a list of years.
    /// Supports single year (e.g. "2025") or range (e.g. "2022...2026").
    /// </summary>
    public static List<int>? ParseYears(string input)
    {
        // Try single year
        if (int.TryParse(input, out int singleYear))
        {
            return new List<int> { singleYear };
        }

        // Try range with "..." (three dots) or "\u2026" (unicode ellipsis)
        string[] separators = { "...", "\u2026" };
        foreach (var sep in separators)
        {
            if (input.Contains(sep))
            {
                var parts = input.Split(new[] { sep }, StringSplitOptions.None);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out int startYear) &&
                    int.TryParse(parts[1], out int endYear))
                {
                    if (startYear > endYear)
                    {
                        (startYear, endYear) = (endYear, startYear);
                    }
                    var years = new List<int>();
                    for (int y = startYear; y <= endYear; y++)
                    {
                        years.Add(y);
                    }
                    return years;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a comma-separated email string into a list of emails.
    /// </summary>
    public static List<string> ParseEmails(string input)
    {
        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Prints usage information to the console.
    /// </summary>
    public static void PrintUsage()
    {
        Console.WriteLine("Usage: git_heatmap_generator [options] [year] [email] [repository_path]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -r, --repo <path>        Path to the local Git repository");
        Console.WriteLine("  -y, --year <year|range>  Year (e.g., 2025) or range (e.g., 2022...2026)");
        Console.WriteLine("  -e, --email <email>      User email (can be comma-separated or used multiple times)");
        Console.WriteLine("  -o, --output <folder>    Output path or folder for the generated image (default: current directory)");
        Console.WriteLine("  -l, --layout <type>      Layout: vertical (default), horizontal, separate");
        Console.WriteLine("  -f, --format <type>      Output format: png (default), svg");
        Console.WriteLine("  -pr, --pull-requests      Include pull requests in the calculation");
        Console.WriteLine("  -h, --help               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  git_heatmap_generator 2025 user@example.com /path/to/repo");
        Console.WriteLine("  git_heatmap_generator -r /path/to/repo -y 2022...2026 -e user@example.com");
        Console.WriteLine("  git_heatmap_generator -y 2024 -y 2025 -e dev1@mail.com -e dev2@mail.com /path/to/repo");
    }
}
