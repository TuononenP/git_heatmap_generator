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

        foreach (var repoPath in parsed.RepositoryPaths)
        {
            if (!Repository.IsValid(repoPath))
            {
                Console.WriteLine($"Invalid repository path: {repoPath}");
                return;
            }
        }

        string yearDisplay = parsed.Years.Count == 1
            ? parsed.Years[0].ToString()
            : $"{parsed.Years.Min()}-{parsed.Years.Max()}";
        string emailDisplay = string.Join(", ", parsed.Emails);
        string repoDisplay = string.Join(", ", parsed.RepositoryPaths);
        
        Console.WriteLine($"Scanning repositories: {repoDisplay}");
        Console.WriteLine($"Looking for {emailDisplay} in {yearDisplay}...");
        Console.WriteLine($"Layout: {parsed.Layout}");
        if (parsed.IncludePullRequests) Console.WriteLine("Including pull requests (merge commits).");

        var commitCounts = CommitScanner.Scan(parsed.RepositoryPaths, parsed.Emails, parsed.Years, parsed.IncludePullRequests);

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

                string path = HeatmapRenderer.Generate(yearList, parsed.Emails, commitCounts, yearOutputPath, HeatmapLayout.Vertical, parsed.IncludePullRequests, parsed.Format);
                Console.WriteLine($"Heatmap generated for {year}: {path}");
            }
        }
        else
        {
            string outputPath = HeatmapRenderer.Generate(parsed.Years, parsed.Emails, commitCounts, parsed.OutputFolder, parsed.Layout, parsed.IncludePullRequests, parsed.Format);
            Console.WriteLine($"Heatmap generated: {outputPath}");
        }

    }
}
