using LibGit2Sharp;

namespace git_heatmap_generator.Git;

/// <summary>
/// Scans a Git repository for commit activity.
/// </summary>
public static class CommitScanner
{
    /// <summary>
    /// Scans the repositories at the given paths and returns aggregated daily commit counts
    /// for the specified years and email addresses.
    /// This scans ALL branches and tags in each repository.
    /// </summary>
    public static Dictionary<DateTime, int> Scan(List<string> repoPaths, List<string> emails, List<int> years, bool includePrs = false)
    {
        var totalCounts = new Dictionary<DateTime, int>();

        foreach (var repoPath in repoPaths)
        {
            var repoCounts = ScanSingle(repoPath, emails, years, includePrs);
            totalCounts = MergeCounts(totalCounts, repoCounts);
        }

        return totalCounts;
    }

    private static Dictionary<DateTime, int> ScanSingle(string repoPath, List<string> emails, List<int> years, bool includePrs)
    {
        var commitCounts = new Dictionary<DateTime, int>();

        using (var repo = new Repository(repoPath))
        {
            var filter = new CommitFilter 
            { 
                IncludeReachableFrom = repo.Refs,
                SortBy = CommitSortStrategies.Time
            };

            foreach (var commit in repo.Commits.QueryBy(filter))
            {
                if (!includePrs && commit.Parents.Count() > 1)
                    continue;

                bool matchEmail = emails.Any(e =>
                    commit.Author.Email.Equals(e, StringComparison.OrdinalIgnoreCase) ||
                    commit.Committer.Email.Equals(e, StringComparison.OrdinalIgnoreCase));

                if (matchEmail && years.Contains(commit.Author.When.Year))
                {
                    var date = commit.Author.When.Date;
                    if (!commitCounts.ContainsKey(date))
                    {
                        commitCounts[date] = 0;
                    }
                    commitCounts[date]++;
                }
            }
        }

        return commitCounts;
    }

    /// <summary>
    /// Aggregates commit counts from a daily dictionary into per-date counts.
    /// This is a pure function useful for combining data from multiple sources.
    /// </summary>
    public static Dictionary<DateTime, int> MergeCounts(
        Dictionary<DateTime, int> existing,
        Dictionary<DateTime, int> additional)
    {
        var result = new Dictionary<DateTime, int>(existing);
        foreach (var kvp in additional)
        {
            if (result.ContainsKey(kvp.Key))
            {
                result[kvp.Key] += kvp.Value;
            }
            else
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }
}
