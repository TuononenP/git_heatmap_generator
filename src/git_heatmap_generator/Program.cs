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

        if (!Repository.IsValid(parsed.RepositoryPath))
        {
            Console.WriteLine("Invalid repository path.");
            return;
        }

        string yearDisplay = parsed.Years.Count == 1
            ? parsed.Years[0].ToString()
            : $"{parsed.Years.Min()}-{parsed.Years.Max()}";
        string emailDisplay = string.Join(", ", parsed.Emails);
        Console.WriteLine($"Scanning repository {parsed.RepositoryPath} for {emailDisplay} in {yearDisplay}...");
        Console.WriteLine($"Layout: {parsed.Layout}");
        if (parsed.IncludePullRequests) Console.WriteLine("Including pull requests (merge commits).");

        var commitCounts = CommitScanner.Scan(parsed.RepositoryPath, parsed.Emails, parsed.Years, parsed.IncludePullRequests);

        Console.WriteLine($"Found activity on {commitCounts.Count} different days.");

        if (parsed.Layout == HeatmapLayout.Separate)
        {
            foreach (var year in parsed.Years.OrderByDescending(y => y))
            {
                var yearList = new List<int> { year };
                string yearOutputPath = parsed.OutputFolder;
                
                // If the user specified a .png file, we append the year to the filename for separate layout
                if (yearOutputPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    string? directory = Path.GetDirectoryName(yearOutputPath);
                    string fileName = Path.GetFileNameWithoutExtension(yearOutputPath);
                    yearOutputPath = Path.Combine(directory ?? "", $"{fileName}_{year}.png");
                }

                string path = HeatmapRenderer.Generate(yearList, parsed.Emails, commitCounts, yearOutputPath, HeatmapLayout.Vertical, parsed.IncludePullRequests);
                Console.WriteLine($"Heatmap generated for {year}: {path}");
            }
        }
        else
        {
            string outputPath = HeatmapRenderer.Generate(parsed.Years, parsed.Emails, commitCounts, parsed.OutputFolder, parsed.Layout, parsed.IncludePullRequests);
            Console.WriteLine($"Heatmap generated: {outputPath}");
        }

    }
}
