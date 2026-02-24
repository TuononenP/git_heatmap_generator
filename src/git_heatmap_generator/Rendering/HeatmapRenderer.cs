using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using git_heatmap_generator.Models;

namespace git_heatmap_generator.Rendering;

/// <summary>
/// Renders a Git heatmap image from commit data.
/// </summary>
public static class HeatmapRenderer
{
    private const int CellSize = 15;
    private const int CellSpacing = 3;
    private const int Step = CellSize + CellSpacing;
    private const int YearLabelWidth = 70;
    private const int WeekdayLabelWidth = 35;
    private const int LabelAreaWidth = YearLabelWidth + WeekdayLabelWidth;
    
    // Padding and Layout Constants
    private const int Padding = 40; // Equal padding for all sides
    private const int TitleAreaHeight = 90; // Top padding to first grid
    private const int MonthLabelHeight = 20;
    private const int YearGap = 25;
    private const int YearGridHeight = 7 * Step;
    private const int LegendAreaHeight = 60; // From last grid to bottom padding

    /// <summary>
    /// Maps a commit count to the corresponding green color.
    /// </summary>
    public static Color GetColorForCount(int count)
    {
        if (count == 0) return Color.ParseHex("#161b22");
        if (count <= 3) return Color.ParseHex("#0e4429");
        if (count <= 6) return Color.ParseHex("#006d32");
        if (count <= 9) return Color.ParseHex("#26a641");
        return Color.ParseHex("#39d353");
    }

    /// <summary>
    /// Calculates the number of weeks needed to display a given year.
    /// </summary>
    public static int CalculateWeeksForYear(int year)
    {
        DateTime startDate = new DateTime(year, 1, 1);
        DateTime endDate = new DateTime(year, 12, 31);
        int startOffset = (int)startDate.DayOfWeek;
        int totalDays = (endDate - startDate).Days + 1;
        return (int)Math.Ceiling((totalDays + startOffset) / 7.0);
    }

    /// <summary>
    /// Generates the default output filename based on the years and layout.
    /// </summary>
    public static string GetDefaultFileName(List<int> years, HeatmapLayout layout = HeatmapLayout.Vertical)
    {
        if (layout == HeatmapLayout.Horizontal)
        {
            return $"heatmap_horizontal_{years.Min()}-{years.Max()}.png";
        }
        
        return years.Count == 1
            ? $"heatmap_{years[0]}.png"
            : $"heatmap_{years.Min()}-{years.Max()}.png";
    }

    private static (Font? title, Font? label, Font? year) GetFonts()
    {
        FontFamily family = SystemFonts.Families.FirstOrDefault(f => f.Name == "Arial" || f.Name == "Helvetica");
        if (family == default(FontFamily)) family = SystemFonts.Families.FirstOrDefault();
        
        if (family == default(FontFamily)) return (null, null, null);

        return (
            family.CreateFont(20, FontStyle.Bold),
            family.CreateFont(12, FontStyle.Regular),
            family.CreateFont(14, FontStyle.Bold)
        );
    }

    /// <summary>
    /// Resolves the final output path based on user input and default filename.
    /// </summary>
    private static string ResolveOutputPath(string outputPathOrFolder, string defaultFileName)
    {
        if (string.IsNullOrWhiteSpace(outputPathOrFolder))
            return Path.Combine(Environment.CurrentDirectory, defaultFileName);

        if (outputPathOrFolder.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return outputPathOrFolder;

        return Path.Combine(outputPathOrFolder, defaultFileName);
    }

    public static string Generate(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, HeatmapLayout layout = HeatmapLayout.Vertical, bool includePrs = false)
    {
        if (layout == HeatmapLayout.Vertical)
            return GenerateVertical(years, userEmails, commitCounts, outputPathOrFolder, includePrs);
        else
            return GenerateHorizontal(years, userEmails, commitCounts, outputPathOrFolder, includePrs);
    }

    private static string GenerateVertical(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs)
    {
        int yearSectionHeight = MonthLabelHeight + YearGridHeight + YearGap;
        int maxWeeks = years.Max(y => CalculateWeeksForYear(y));

        int width = Padding + LabelAreaWidth + maxWeeks * Step + Padding;
        int height = Padding + TitleAreaHeight + years.Count * yearSectionHeight - YearGap + LegendAreaHeight + Padding;

        var sortedYears = years.OrderByDescending(y => y).ToList();
        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(Color.ParseHex("#0d1117")));
            DrawCommonItems(image, years, userEmails, fontTitle, fontLabel, width, height, includePrs);

            float gridLeft = Padding + LabelAreaWidth;
            float gridTopBase = Padding + TitleAreaHeight;

            for (int yi = 0; yi < sortedYears.Count; yi++)
            {
                int year = sortedYears[yi];
                float sectionTop = gridTopBase + yi * yearSectionHeight;
                DrawYear(image, year, sectionTop, gridLeft, commitCounts, fontYear, fontLabel);
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, HeatmapLayout.Vertical));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }

    private static string GenerateHorizontal(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs)
    {
        var sortedYears = years.OrderBy(y => y).ToList();

        int totalWeeks = years.Sum(y => CalculateWeeksForYear(y));
        int yearGapX = 40;

        int width = Padding + LabelAreaWidth + totalWeeks * Step + (years.Count - 1) * yearGapX + Padding;
        int height = Padding + TitleAreaHeight + MonthLabelHeight + YearGridHeight + LegendAreaHeight + Padding;

        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(Color.ParseHex("#0d1117")));
            DrawCommonItems(image, years, userEmails, fontTitle, fontLabel, width, height, includePrs);

            float gridLeftBase = Padding + LabelAreaWidth;
            float gridTopBase = Padding + TitleAreaHeight;

            float currentGridLeft = gridLeftBase;
            for (int yi = 0; yi < sortedYears.Count; yi++)
            {
                int year = sortedYears[yi];
                int weeks = CalculateWeeksForYear(year);
                
                DrawYear(image, year, gridTopBase, currentGridLeft, commitCounts, fontYear, fontLabel, isHorizontal: true, isFirstYearInHorizontal: yi == 0);
                currentGridLeft += weeks * Step + yearGapX;
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, HeatmapLayout.Horizontal));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }

    private static void DrawYear(Image<Rgba32> image, int year, float sectionTop, float gridLeft, 
        Dictionary<DateTime, int> commitCounts, Font? fontYear, Font? fontLabel, bool isHorizontal = false, bool isFirstYearInHorizontal = false)
    {
        float gridTop = sectionTop + MonthLabelHeight;
        DateTime startDate = new DateTime(year, 1, 1);
        int startOffset = (int)startDate.DayOfWeek;
        int totalDays = (new DateTime(year, 12, 31) - startDate).Days + 1;

        int yearTotal = commitCounts.Where(kv => kv.Key.Year == year).Sum(kv => kv.Value);

        // Year and Total labels
        if (fontYear != null)
        {
            float x = isHorizontal ? gridLeft : Padding;
            float y = isHorizontal ? sectionTop - 20 : gridTop + 3 * Step;
            image.Mutate(ctx => ctx.DrawText(year.ToString(), fontYear, Color.White, new PointF(x, y)));
            
            if (fontLabel != null)
            {
                string countText = $"{yearTotal} total";
                image.Mutate(ctx => ctx.DrawText(countText, fontLabel, Color.ParseHex("#7d8590"), 
                    new PointF(x, y + (isHorizontal ? 18 : 20))));
            }
        }

        // Weekday labels
        if (fontLabel != null && (!isHorizontal || isFirstYearInHorizontal))
        {
            string[] days = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            float x = Padding + (isHorizontal ? 0 : YearLabelWidth);
            for (int i = 0; i < 7; i++)
            {
                image.Mutate(ctx => ctx.DrawText(days[i], fontLabel, Color.ParseHex("#7d8590"),
                    new PointF(x, gridTop + i * Step)));
            }
        }

        // Cells and month labels
        int currentMonth = 0;
        for (int i = 0; i < totalDays; i++)
        {
            DateTime currentDate = startDate.AddDays(i);
            int weekIndex = (i + startOffset) / 7;
            int dayOfWeek = (int)currentDate.DayOfWeek;

            if (currentDate.Month != currentMonth)
            {
                currentMonth = currentDate.Month;
                if (fontLabel != null && currentDate.Day <= 14)
                {
                    string monthStr = currentDate.ToString("MMM");
                    image.Mutate(ctx => ctx.DrawText(monthStr, fontLabel, Color.ParseHex("#7d8590"),
                        new PointF(gridLeft + weekIndex * Step, sectionTop)));
                }
            }

            int count = commitCounts.GetValueOrDefault(currentDate, 0);
            Color cellColor = GetColorForCount(count);
            var rect = new RectangleF(gridLeft + weekIndex * Step, gridTop + dayOfWeek * Step, CellSize, CellSize);
            image.Mutate(ctx => ctx.Fill(cellColor, rect));
        }
    }

    private static void DrawCommonItems(Image<Rgba32> image, List<int> years, List<string> userEmails, 
        Font? fontTitle, Font? fontLabel, int width, int height, bool includePrs = false)
    {
        if (fontTitle != null)
        {
            string emailDisplay = string.Join(", ", userEmails);
            string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
            
            image.Mutate(x => x.DrawText(emailDisplay, fontTitle, Color.White, new PointF(Padding, Padding)));
            image.Mutate(x => x.DrawText(yearDisplay, fontTitle, Color.ParseHex("#7d8590"), new PointF(Padding, Padding + 35)));
        }

        if (fontLabel != null)
        {
            float legendY = height - Padding - 20;
            string labelText = includePrs ? "Amount of commits and pull requests" : "Amount of commits";
            
            image.Mutate(x => x.DrawText(labelText, 
                fontLabel, Color.ParseHex("#7d8590"), new PointF(Padding, legendY)));

            float legendX = width - 240 - Padding;
            image.Mutate(x => x.DrawText("Less", fontLabel, Color.ParseHex("#7d8590"), new PointF(legendX, legendY)));
            legendX += 40;
            for (int i = 0; i < 5; i++)
            {
                image.Mutate(x => x.Fill(GetColorForCount(i * 3), new RectangleF(legendX, legendY + 2, CellSize, CellSize)));
                legendX += Step;
            }
            image.Mutate(x => x.DrawText("More", fontLabel, Color.ParseHex("#7d8590"), new PointF(legendX + 5, legendY)));
        }
    }

    private static void SaveImage(Image<Rgba32> image, string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        image.Save(fullPath);
    }
}
