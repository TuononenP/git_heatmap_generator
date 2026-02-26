using git_heatmap_generator.Cli;
using git_heatmap_generator.Git;
using git_heatmap_generator.Models;
using git_heatmap_generator.Rendering;
using LibGit2Sharp;

namespace git_heatmap_generator;

class Program
{
    static void Main(string[] args)
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        var parsed = ArgumentParser.Parse(args);

        if (parsed == null)
        {
            ArgumentParser.PrintUsage();
            return;
        }

        if (parsed.ShowHelp)
        {
            ArgumentParser.PrintUsage();
            return;
        }

        foreach (var repoPath in parsed.RepositoryPaths)
        {
            if (!Repository.IsValid(repoPath))
            {
                Console.WriteLine($"Invalid repository path: {repoPath}");
                return;
            }
        }

        var commitCounts = CommitScanner.Scan(parsed.RepositoryPaths, parsed.Emails, parsed.Years.Count > 0 ? parsed.Years : null, parsed.IncludePullRequests);

        // If no years were specified, populate them from the scan results
        if (parsed.Years.Count == 0)
        {
            parsed.Years = commitCounts.Keys.Select(d => d.Year).Distinct().OrderBy(y => y).ToList();
        }

        if (parsed.Years.Count == 0)
        {
            Console.WriteLine("No activity found for the specified emails.");
            return;
        }

        string yearDisplay = parsed.Years.Count == 1
            ? parsed.Years[0].ToString()
            : $"{parsed.Years.Min()}-{parsed.Years.Max()}";
        string emailDisplay = string.Join(", ", parsed.Emails);
        string repoDisplay = string.Join(", ", parsed.RepositoryPaths);
        
        Console.WriteLine($"Scanning repositories: {repoDisplay}");
        Console.WriteLine($"Looking for {emailDisplay} in {yearDisplay}...");
        Console.WriteLine($"Layout: {parsed.Layout}");
        Console.WriteLine($"Style: {parsed.Theme}");
        Console.WriteLine($"Mode: {parsed.Mode}");
        if (parsed.IncludePullRequests) Console.WriteLine("Including pull requests (merge commits).");

        Console.WriteLine($"Found activity on {commitCounts.Count} different days.");

        if (parsed.Layout == HeatmapLayout.Separate)
        {
            foreach (var year in parsed.Years.OrderByDescending(y => y))
            {
                var yearList = new List<int> { year };
                string yearOutputPath = parsed.OutputFolder;
                
                // If the user specified a file path, we append the year to the filename for separate layout
                if (yearOutputPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
                    yearOutputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    string? directory = Path.GetDirectoryName(yearOutputPath);
                    string fileName = Path.GetFileNameWithoutExtension(yearOutputPath);
                    string extension = Path.GetExtension(yearOutputPath);
                    yearOutputPath = Path.Combine(directory ?? "", $"{fileName}_{year}{extension}");
                }

                string path = HeatmapRenderer.Generate(yearList, parsed.Emails, commitCounts, yearOutputPath, HeatmapLayout.Vertical, parsed.IncludePullRequests, parsed.Format, parsed.Theme, parsed.Mode, parsed.CustomColors, parsed.Use3DStyle, parsed.Use3DChart, parsed.Title);
                Console.WriteLine($"Heatmap generated for {year}: {path}");
            }
        }
        else
        {
            string outputPath = HeatmapRenderer.Generate(parsed.Years, parsed.Emails, commitCounts, parsed.OutputFolder, parsed.Layout, parsed.IncludePullRequests, parsed.Format, parsed.Theme, parsed.Mode, parsed.CustomColors, parsed.Use3DStyle, parsed.Use3DChart, parsed.Title);
            Console.WriteLine($"Heatmap generated: {outputPath}");
        }

    }
}
