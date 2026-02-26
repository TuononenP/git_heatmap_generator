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
    /// Generates the default output filename based on the years and layout.
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
    public static string GetDefaultFileName(List<int> years, HeatmapLayout layout = HeatmapLayout.Vertical, OutputFormat format = OutputFormat.Png)
    {
        string extension = format == OutputFormat.Svg ? "svg" : "png";
        
        if (layout == HeatmapLayout.Horizontal)
        {
            return $"heatmap_horizontal_{years.Min()}-{years.Max()}.{extension}";
        }
        
        return years.Count == 1
            ? $"heatmap_{years[0]}.{extension}"
            : $"heatmap_{years.Min()}-{years.Max()}.{extension}";
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

        if (outputPathOrFolder.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || 
            outputPathOrFolder.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return outputPathOrFolder;

        return Path.Combine(outputPathOrFolder, defaultFileName);
    }

    public static string Generate(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, HeatmapLayout layout = HeatmapLayout.Vertical, bool includePrs = false, OutputFormat format = OutputFormat.Png, ColorTheme theme = ColorTheme.Default, ColorMode mode = ColorMode.Dark, List<string>? customColors = null)
    {
        var colorScheme = ColorScheme.GetTheme(theme, mode, customColors);
        if (format == OutputFormat.Svg)
            return GenerateSvg(years, userEmails, commitCounts, outputPathOrFolder, layout, includePrs, colorScheme);

        if (layout == HeatmapLayout.Vertical)
            return GenerateVertical(years, userEmails, commitCounts, outputPathOrFolder, includePrs, colorScheme);
        else
            return GenerateHorizontal(years, userEmails, commitCounts, outputPathOrFolder, includePrs, colorScheme);
    }

    private static string GenerateVertical(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs, ColorScheme colorScheme)
    {
        int yearSectionHeight = MonthLabelHeight + YearGridHeight + YearGap;
        int maxWeeks = years.Max(y => CalculateWeeksForYear(y));

        int width = Padding + LabelAreaWidth + maxWeeks * Step + Padding;
        int height = Padding + TitleAreaHeight + years.Count * yearSectionHeight - YearGap + LegendAreaHeight + Padding;

        var sortedYears = years.OrderByDescending(y => y).ToList();
        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(colorScheme.BackgroundColor));
            DrawCommonItems(image, years, userEmails, fontTitle, fontLabel, width, height, includePrs, colorScheme);

            float gridLeft = Padding + LabelAreaWidth;
            float gridTopBase = Padding + TitleAreaHeight;

            for (int yi = 0; yi < sortedYears.Count; yi++)
            {
                int year = sortedYears[yi];
                float sectionTop = gridTopBase + yi * yearSectionHeight;
                DrawYear(image, year, sectionTop, gridLeft, commitCounts, fontYear, fontLabel, colorScheme);
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, HeatmapLayout.Vertical));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }

    private static string GenerateHorizontal(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs, ColorScheme colorScheme)
    {
        var sortedYears = years.OrderBy(y => y).ToList();

        int totalWeeks = years.Sum(y => CalculateWeeksForYear(y));
        int yearGapX = 40;
        int horizontalTitleArea = TitleAreaHeight + 20; // Extra space for year labels in horizontal layout

        int width = Padding + LabelAreaWidth + totalWeeks * Step + (years.Count - 1) * yearGapX + Padding;
        int height = Padding + horizontalTitleArea + MonthLabelHeight + YearGridHeight + LegendAreaHeight + Padding;

        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(colorScheme.BackgroundColor));
            DrawCommonItems(image, years, userEmails, fontTitle, fontLabel, width, height, includePrs, colorScheme);

            float gridLeftBase = Padding + LabelAreaWidth;
            float gridTopBase = Padding + horizontalTitleArea;

            float currentGridLeft = gridLeftBase;
            for (int yi = 0; yi < sortedYears.Count; yi++)
            {
                int year = sortedYears[yi];
                int weeks = CalculateWeeksForYear(year);
                
                DrawYear(image, year, gridTopBase, currentGridLeft, commitCounts, fontYear, fontLabel, colorScheme, isHorizontal: true, isFirstYearInHorizontal: yi == 0);
                currentGridLeft += weeks * Step + yearGapX;
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, HeatmapLayout.Horizontal));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }

    private static void DrawYear(Image<Rgba32> image, int year, float sectionTop, float gridLeft, 
        Dictionary<DateTime, int> commitCounts, Font? fontYear, Font? fontLabel, ColorScheme colorScheme, bool isHorizontal = false, bool isFirstYearInHorizontal = false)
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
            float y = isHorizontal ? sectionTop - 42 : gridTop + 3 * Step;
            image.Mutate(ctx => ctx.DrawText(year.ToString(), fontYear, colorScheme.TextColor, new PointF(x, y)));
            
            if (fontLabel != null)
            {
                string countText = $"{yearTotal} total";
                image.Mutate(ctx => ctx.DrawText(countText, fontLabel, colorScheme.SubtextColor, 
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
                image.Mutate(ctx => ctx.DrawText(days[i], fontLabel, colorScheme.SubtextColor,
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
                    image.Mutate(ctx => ctx.DrawText(monthStr, fontLabel, colorScheme.SubtextColor,
                        new PointF(gridLeft + weekIndex * Step, sectionTop)));
                }
            }

            int count = commitCounts.GetValueOrDefault(currentDate, 0);
            Color cellColor = colorScheme.GetColorForCount(count);
            var rect = new RectangleF(gridLeft + weekIndex * Step, gridTop + dayOfWeek * Step, CellSize, CellSize);
            image.Mutate(ctx => ctx.Fill(cellColor, rect));
        }
    }

    private static void DrawCommonItems(Image<Rgba32> image, List<int> years, List<string> userEmails, 
        Font? fontTitle, Font? fontLabel, int width, int height, bool includePrs, ColorScheme colorScheme)
    {
        if (fontTitle != null)
        {
            string emailDisplay = string.Join(", ", userEmails);
            string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
            
            image.Mutate(x => x.DrawText(emailDisplay, fontTitle, colorScheme.TextColor, new PointF(Padding, Padding)));
            image.Mutate(x => x.DrawText(yearDisplay, fontTitle, colorScheme.SubtextColor, new PointF(Padding, Padding + 35)));
        }

        if (fontLabel != null)
        {
            float legendY = height - Padding - 20;
            string labelText = includePrs ? "Amount of commits and pull requests" : "Amount of commits";
            
            image.Mutate(x => x.DrawText(labelText, 
                fontLabel, colorScheme.SubtextColor, new PointF(Padding, legendY)));

            float legendX = width - 240 - Padding;
            image.Mutate(x => x.DrawText("Less", fontLabel, colorScheme.SubtextColor, new PointF(legendX, legendY)));
            legendX += 40;
            for (int i = 0; i < 5; i++)
            {
                image.Mutate(x => x.Fill(colorScheme.GetColorForCount(i * 3), new RectangleF(legendX, legendY + 2, CellSize, CellSize)));
                legendX += Step;
            }
            image.Mutate(x => x.DrawText("More", fontLabel, colorScheme.SubtextColor, new PointF(legendX + 5, legendY)));
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

    private static string GenerateSvg(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, HeatmapLayout layout, bool includePrs, ColorScheme colorScheme)
    {
        int maxWeeks = years.Max(y => CalculateWeeksForYear(y));
        int totalWeeks = years.Sum(y => CalculateWeeksForYear(y));
        int yearSectionHeight = MonthLabelHeight + YearGridHeight + YearGap;
        int yearGapX = 40;
        int horizontalTitleArea = TitleAreaHeight + 20;

        int width, height;
        if (layout == HeatmapLayout.Horizontal)
        {
            width = Padding + LabelAreaWidth + totalWeeks * Step + (years.Count - 1) * yearGapX + Padding;
            height = Padding + horizontalTitleArea + MonthLabelHeight + YearGridHeight + LegendAreaHeight + Padding;
        }
        else
        {
            width = Padding + LabelAreaWidth + maxWeeks * Step + Padding;
            height = Padding + TitleAreaHeight + years.Count * yearSectionHeight - YearGap + LegendAreaHeight + Padding;
        }

        using var writer = new StringWriter();
        string bgHex = colorScheme.BackgroundColor.ToPixel<Rgba32>().ToHex().Substring(0, 6);
        string textHex = colorScheme.TextColor.ToPixel<Rgba32>().ToHex().Substring(0, 6);
        string subtextHex = colorScheme.SubtextColor.ToPixel<Rgba32>().ToHex().Substring(0, 6);

        writer.WriteLine($"<svg width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");
        writer.WriteLine($"  <rect width=\"100%\" height=\"100%\" fill=\"#{bgHex}\" />");

        // Common text styles
        writer.WriteLine("  <style>");
        writer.WriteLine($"    .title {{ fill: #{textHex}; font-family: Arial, Helvetica, sans-serif; font-size: 20px; font-weight: bold; }}");
        writer.WriteLine($"    .subtitle {{ fill: #{subtextHex}; font-family: Arial, Helvetica, sans-serif; font-size: 20px; font-weight: bold; }}");
        writer.WriteLine($"    .label {{ fill: #{subtextHex}; font-family: Arial, Helvetica, sans-serif; font-size: 12px; }}");
        writer.WriteLine($"    .year-label {{ fill: #{textHex}; font-family: Arial, Helvetica, sans-serif; font-size: 14px; font-weight: bold; }}");
        writer.WriteLine("  </style>");

        // Title and Year Range
        string emailDisplay = System.Net.WebUtility.HtmlEncode(string.Join(", ", userEmails));
        string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{Padding + 20}\" class=\"title\">{emailDisplay}</text>");
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{Padding + 55}\" class=\"subtitle\">{yearDisplay}</text>");

        float gridLeftBase = Padding + LabelAreaWidth;
        float gridTopBase = layout == HeatmapLayout.Horizontal ? Padding + horizontalTitleArea : Padding + TitleAreaHeight;

        var sortedYears = layout == HeatmapLayout.Horizontal 
            ? years.OrderBy(y => y).ToList() 
            : years.OrderByDescending(y => y).ToList();

        float currentGridLeft = gridLeftBase;
        for (int yi = 0; yi < sortedYears.Count; yi++)
        {
            int year = sortedYears[yi];
            float sectionTop = layout == HeatmapLayout.Horizontal ? gridTopBase : gridTopBase + yi * yearSectionHeight;
            float gridLeft = layout == HeatmapLayout.Horizontal ? currentGridLeft : gridLeftBase;
            
            DrawSvgYear(writer, year, sectionTop, gridLeft, commitCounts, layout == HeatmapLayout.Horizontal, yi == 0, colorScheme);
            
            if (layout == HeatmapLayout.Horizontal)
                currentGridLeft += CalculateWeeksForYear(year) * Step + yearGapX;
        }

        // Legend
        float legendY = height - Padding - 20;
        string labelText = includePrs ? "Amount of commits and pull requests" : "Amount of commits";
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{legendY + 12}\" class=\"label\">{labelText}</text>");

        float legendX = width - 240 - Padding;
        writer.WriteLine($"  <text x=\"{legendX}\" y=\"{legendY + 12}\" class=\"label\">Less</text>");
        legendX += 40;
        for (int i = 0; i < 5; i++)
        {
            Color c = colorScheme.GetColorForCount(i * 3);
            var rgba = c.ToPixel<Rgba32>();
            string hex = $"#{rgba.R:X2}{rgba.G:X2}{rgba.B:X2}";
            writer.WriteLine($"  <rect x=\"{legendX}\" y=\"{legendY}\" width=\"{CellSize}\" height=\"{CellSize}\" fill=\"{hex}\" rx=\"2\" ry=\"2\" />");
            legendX += Step;
        }
        writer.WriteLine($"  <text x=\"{legendX + 5}\" y=\"{legendY + 12}\" class=\"label\">More</text>");

        writer.WriteLine("</svg>");

        string defaultFileName = GetDefaultFileName(years, layout, OutputFormat.Svg);
        string finalPath = ResolveOutputPath(outputPathOrFolder, defaultFileName);
        
        string? dir = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        File.WriteAllText(finalPath, writer.ToString());
        return finalPath;
    }

    private static void DrawSvgYear(StringWriter writer, int year, float sectionTop, float gridLeft, 
        Dictionary<DateTime, int> commitCounts, bool isHorizontal, bool isFirstYearInHorizontal, ColorScheme colorScheme)
    {
        float gridTop = sectionTop + MonthLabelHeight;
        DateTime startDate = new DateTime(year, 1, 1);
        int startOffset = (int)startDate.DayOfWeek;
        int totalDays = (new DateTime(year, 12, 31) - startDate).Days + 1;
        int yearTotal = commitCounts.Where(kv => kv.Key.Year == year).Sum(kv => kv.Value);

        // Year and Total labels
        float lx = isHorizontal ? gridLeft : Padding;
        float ly = isHorizontal ? sectionTop - 42 : gridTop + 3 * Step;
        writer.WriteLine($"  <text x=\"{lx}\" y=\"{ly + 14}\" class=\"year-label\">{year}</text>");
        writer.WriteLine($"  <text x=\"{lx}\" y=\"{ly + (isHorizontal ? 18 : 20) + 12}\" class=\"label\">{yearTotal} total</text>");

        // Weekday labels
        if (!isHorizontal || isFirstYearInHorizontal)
        {
            string[] days = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            float dx = Padding + (isHorizontal ? 0 : YearLabelWidth);
            for (int i = 0; i < 7; i++)
            {
                writer.WriteLine($"  <text x=\"{dx}\" y=\"{gridTop + i * Step + 12}\" class=\"label\">{days[i]}</text>");
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
                if (currentDate.Day <= 14)
                {
                    string monthStr = currentDate.ToString("MMM");
                    writer.WriteLine($"  <text x=\"{gridLeft + weekIndex * Step}\" y=\"{sectionTop + 12}\" class=\"label\">{monthStr}</text>");
                }
            }

            int count = commitCounts.GetValueOrDefault(currentDate, 0);
            Color c = colorScheme.GetColorForCount(count);
            var rgba = c.ToPixel<Rgba32>();
            string hex = $"#{rgba.R:X2}{rgba.G:X2}{rgba.B:X2}";
            writer.WriteLine($"  <rect x=\"{gridLeft + weekIndex * Step}\" y=\"{gridTop + dayOfWeek * Step}\" width=\"{CellSize}\" height=\"{CellSize}\" fill=\"{hex}\" rx=\"2\" ry=\"2\" />");
        }
    }
}

