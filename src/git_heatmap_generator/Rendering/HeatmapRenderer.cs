using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using git_heatmap_generator.Models;
using Path = System.IO.Path;

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
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, HeatmapLayout layout = HeatmapLayout.Vertical, bool includePrs = false, OutputFormat format = OutputFormat.Png, ColorTheme theme = ColorTheme.Default, ColorMode mode = ColorMode.Dark, List<string>? customColors = null, bool use3D = false, bool use3DChart = false, string? customTitle = null, bool hideStats = false)
    {
        var colorScheme = ColorScheme.GetTheme(theme, mode, customColors);
        
        if (use3DChart)
        {
            if (format == OutputFormat.Svg)
                return Generate3DSvgChart(years, userEmails, commitCounts, outputPathOrFolder, includePrs, colorScheme, customTitle, hideStats);
            return Generate3DChart(years, userEmails, commitCounts, outputPathOrFolder, includePrs, colorScheme, customTitle, hideStats);
        }

        if (format == OutputFormat.Svg)
            return GenerateSvg(years, userEmails, commitCounts, outputPathOrFolder, layout, includePrs, colorScheme, use3D, customTitle, hideStats);

        if (layout == HeatmapLayout.Vertical)
            return GenerateVertical(years, userEmails, commitCounts, outputPathOrFolder, includePrs, colorScheme, use3D, customTitle, hideStats);
        else
            return GenerateHorizontal(years, userEmails, commitCounts, outputPathOrFolder, includePrs, colorScheme, use3D, customTitle, hideStats);
    }

    private static string Generate3DChart(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs, ColorScheme colorScheme, string? customTitle, bool hideStats)
    {
        // Isometric Constants
        const float isoW = 14;
        const float isoH = 7;
        const float barScale = 6;
        const float minBarHeight = 3;
        const float cellGap = 1.5f;

        // Grid boundaries: week range [0, 52], day range [0, 6]
        // cx = (week - day) * isoW => range [-6, 52] * isoW
        // cy = (week + day) * isoH => range [0, 58] * isoH
        int gridWidth = (int)(58 * isoW);
        int gridHeight = (int)(58 * isoH);

        int statsHeight = hideStats ? 0 : 200;
        int yearStepY = 160; // How much each year adds to total height (vertical overlap for compactness)
        int width = gridWidth + Padding * 2 + 120;
        if (!hideStats) width = Math.Max(width, Padding + 750 + Padding);
        int height = Padding + TitleAreaHeight + statsHeight + (years.Count - 1) * yearStepY + gridHeight + 120 + Padding;

        var sortedYears = years.OrderByDescending(y => y).ToList(); // Draw newest first (back-to-front)
        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(colorScheme.BackgroundColor));

            if (fontTitle != null)
            {
                string titleDisplay = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : string.Join(", ", userEmails);
                string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
                image.Mutate(x => x.DrawText(titleDisplay, fontTitle, colorScheme.TextColor, new PointF(Padding, Padding)));
                image.Mutate(x => x.DrawText($"{yearDisplay}", fontTitle, colorScheme.SubtextColor, new PointF(Padding, Padding + 35)));
            }

            if (!hideStats)
            {
                var stats = DashboardStats.Calculate(commitCounts, years);
                float statsTop = Padding + TitleAreaHeight - 10;
                DrawStats(image, stats, statsTop, Padding, fontTitle, fontLabel, colorScheme);
            }

            float originX = Padding + 100 + 6 * isoW; // Correct for min cx offset
            float currentOriginY = Padding + TitleAreaHeight + statsHeight + 60;

            foreach (var year in sortedYears)
            {
                if (fontYear != null)
                {
                    image.Mutate(x => x.DrawText(year.ToString(), fontYear, colorScheme.TextColor, new PointF(Padding, currentOriginY + 20)));
                }

                DateTime startDate = new DateTime(year, 1, 1);
                int totalDays = (new DateTime(year, 12, 31) - startDate).Days + 1;
                int startOffset = (int)startDate.DayOfWeek;

                // Track month labels
                int currentMonth = 0;
                var monthLabels = new List<(string Text, float X, float Y)>();

                for (int i = 0; i < totalDays; i++)
                {
                    DateTime currentDate = startDate.AddDays(i);
                    int week = (i + startOffset) / 7;
                    int day = (int)currentDate.DayOfWeek;

                    // Draw month labels for isometric view
                    if (currentDate.Month != currentMonth && fontLabel != null)
                    {
                        currentMonth = currentDate.Month;
                        string monthStr = currentDate.ToString("MMM");
                        float monthX = originX + week * isoW;
                        float monthY = currentOriginY + week * isoH - 35;
                        monthLabels.Add((monthStr, monthX, monthY));
                    }

                    float cx = originX + (week - day) * isoW;
                    float cy = currentOriginY + (week + day) * isoH;

                    int count = commitCounts.GetValueOrDefault(currentDate, 0);
                    Color baseColor = colorScheme.GetColorForCount(count);

                    // Height proportional to count with a cap
                    float barHeight = count == 0 ? 2 : Math.Min(40, count) * barScale / 2.5f + minBarHeight;

                    DrawIsometricBar(image, cx, cy, isoW - cellGap, isoH - cellGap, barHeight, baseColor);
                }

                // Draw month labels after bars to ensure they are on top
                foreach (var label in monthLabels)
                {
                    image.Mutate(x => x.DrawText(label.Text, fontLabel, colorScheme.SubtextColor, new PointF(label.X, label.Y)));
                }

                currentOriginY += yearStepY;
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, HeatmapLayout.Vertical));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }

    private static void DrawIsometricBar(Image<Rgba32> image, float x, float y, float w, float h, float height, Color color)
    {
        // Points for the faces
        var topPoints = new PointF[] {
            new PointF(x, y - height),
            new PointF(x + w, y + h - height),
            new PointF(x, y + 2 * h - height),
            new PointF(x - w, y + h - height)
        };

        var leftPoints = new PointF[] {
            new PointF(x - w, y + h - height),
            new PointF(x, y + 2 * h - height),
            new PointF(x, y + 2 * h),
            new PointF(x - w, y + h)
        };

        var rightPoints = new PointF[] {
            new PointF(x, y + 2 * h - height),
            new PointF(x + w, y + h - height),
            new PointF(x + w, y + h),
            new PointF(x, y + 2 * h)
        };

        Color leftColor = Darken(color, 0.85f);
        Color rightColor = Darken(color, 0.70f);

        image.Mutate(ctx => ctx.Fill(leftColor, new Polygon(leftPoints)));
        image.Mutate(ctx => ctx.Fill(rightColor, new Polygon(rightPoints)));
        image.Mutate(ctx => ctx.Fill(color, new Polygon(topPoints)));
        
        // Add a very subtle outline to separate same-colored bars
        var outlineColor = Color.FromRgba(255, 255, 255, 15);
        image.Mutate(ctx => ctx.Draw(outlineColor, 1f, new Polygon(topPoints)));
    }

    private static string GenerateVertical(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs, ColorScheme colorScheme, bool use3D, string? customTitle, bool hideStats, HeatmapLayout layout = HeatmapLayout.Vertical)
    {
        int statsHeight = hideStats ? 0 : 200;
        int yearSectionHeight = MonthLabelHeight + YearGridHeight + YearGap;
        int maxWeeks = years.Max(y => CalculateWeeksForYear(y));

        int width = Padding + LabelAreaWidth + maxWeeks * Step + Padding;
        if (!hideStats) width = Math.Max(width, Padding + 750 + Padding);
        
        int height = Padding + TitleAreaHeight + statsHeight + years.Count * yearSectionHeight - YearGap + LegendAreaHeight + Padding;

        var sortedYears = years.OrderByDescending(y => y).ToList();
        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(colorScheme.BackgroundColor));
            DrawCommonItems(image, years, userEmails, fontTitle, fontLabel, width, height, includePrs, colorScheme, use3D, customTitle);

            if (!hideStats)
            {
                var stats = DashboardStats.Calculate(commitCounts, years);
                float statsTop = Padding + TitleAreaHeight - 10;
                DrawStats(image, stats, statsTop, Padding, fontTitle, fontLabel, colorScheme);
            }

            float gridLeft = Padding + LabelAreaWidth;
            float gridTopBase = Padding + TitleAreaHeight + statsHeight;

            for (int yi = 0; yi < sortedYears.Count; yi++)
            {
                int year = sortedYears[yi];
                float sectionTop = gridTopBase + yi * yearSectionHeight;
                DrawYear(image, year, sectionTop, gridLeft, commitCounts, fontYear, fontLabel, colorScheme, use3D: use3D);
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, layout));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }


    private static void DrawStats(Image<Rgba32> image, DashboardStats stats, float top, float left, Font? fontTitle, Font? fontLabel, ColorScheme colorScheme)
    {
        if (fontTitle == null || fontLabel == null) return;

        float currentX = left;
        float boxWidth = 150;
        float rowHeight = 70;
        
        // Row 1
        DrawStatBox(image, "Total Commits", stats.TotalCommits.ToString(), currentX, top, boxWidth, fontTitle, fontLabel, colorScheme);
        currentX += boxWidth;
        DrawStatBox(image, "Active Days", stats.ActiveDays.ToString(), currentX, top, boxWidth, fontTitle, fontLabel, colorScheme);
        currentX += boxWidth;
        DrawStatBox(image, "Max per Day", stats.MaxCommitsPerDay.ToString(), currentX, top, boxWidth, fontTitle, fontLabel, colorScheme);
        currentX += boxWidth;
        DrawStatBox(image, "Avg per Active Day", stats.AverageCommitsPerActiveDay.ToString("F1"), currentX, top, boxWidth, fontTitle, fontLabel, colorScheme);

        // Row 2
        currentX = left;
        float secondRowTop = top + rowHeight;
        DrawStatBox(image, "Longest Streak", $"{stats.LongestStreak} days", currentX, secondRowTop, boxWidth, fontTitle, fontLabel, colorScheme);
        currentX += boxWidth;
        DrawStatBox(image, "Most Active Day", stats.MostActiveDayOfWeek.ToString(), currentX, secondRowTop, boxWidth, fontTitle, fontLabel, colorScheme);
        currentX += boxWidth;
        DrawStatBox(image, "Most Active Month", stats.MostActiveMonth, currentX, secondRowTop, boxWidth, fontTitle, fontLabel, colorScheme);
    }

    private static void DrawStatBox(Image<Rgba32> image, string label, string value, float x, float y, float width, Font fontTitle, Font fontLabel, ColorScheme colorScheme)
    {
        image.Mutate(ctx => ctx.DrawText(label, fontLabel, colorScheme.SubtextColor, new PointF(x, y)));
        image.Mutate(ctx => ctx.DrawText(value, fontTitle, colorScheme.TextColor, new PointF(x, y + 25)));
    }

    private static string GenerateHorizontal(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs, ColorScheme colorScheme, bool use3D, string? customTitle, bool hideStats)
    {
        var sortedYears = years.OrderBy(y => y).ToList();

        int statsHeight = hideStats ? 0 : 200;
        int totalWeeks = years.Sum(y => CalculateWeeksForYear(y));
        int yearGapX = 40;
        int horizontalTitleArea = TitleAreaHeight + 20; // Extra space for year labels in horizontal layout

        int width = Padding + LabelAreaWidth + totalWeeks * Step + (years.Count - 1) * yearGapX + Padding;
        if (!hideStats) width = Math.Max(width, Padding + 750 + Padding);
        
        int height = Padding + horizontalTitleArea + statsHeight + MonthLabelHeight + YearGridHeight + LegendAreaHeight + Padding;

        var (fontTitle, fontLabel, fontYear) = GetFonts();

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            image.Mutate(x => x.Fill(colorScheme.BackgroundColor));
            DrawCommonItems(image, years, userEmails, fontTitle, fontLabel, width, height, includePrs, colorScheme, use3D, customTitle);

            if (!hideStats)
            {
                var stats = DashboardStats.Calculate(commitCounts, years);
                float statsTop = Padding + TitleAreaHeight - 10;
                DrawStats(image, stats, statsTop, Padding, fontTitle, fontLabel, colorScheme);
            }

            float gridLeftBase = Padding + LabelAreaWidth;
            float gridTopBase = Padding + horizontalTitleArea + statsHeight;

            float currentGridLeft = gridLeftBase;
            for (int yi = 0; yi < sortedYears.Count; yi++)
            {
                int year = sortedYears[yi];
                int weeks = CalculateWeeksForYear(year);
                
                DrawYear(image, year, gridTopBase, currentGridLeft, commitCounts, fontYear, fontLabel, colorScheme, isHorizontal: true, isFirstYearInHorizontal: yi == 0, use3D: use3D);
                currentGridLeft += weeks * Step + yearGapX;
            }

            string finalPath = ResolveOutputPath(outputPathOrFolder, GetDefaultFileName(years, HeatmapLayout.Horizontal));
            SaveImage(image, finalPath);
            return finalPath;
        }
    }

    private static void DrawYear(Image<Rgba32> image, int year, float sectionTop, float gridLeft, 
        Dictionary<DateTime, int> commitCounts, Font? fontYear, Font? fontLabel, ColorScheme colorScheme, bool isHorizontal = false, bool isFirstYearInHorizontal = false, bool use3D = false)
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
            DrawCell(image, gridLeft + weekIndex * Step, gridTop + dayOfWeek * Step, CellSize, cellColor, use3D, count);
        }
    }

    private static Color Darken(Color color, float factor)
    {
        var rgba = color.ToPixel<Rgba32>();
        return Color.FromRgba(
            (byte)(rgba.R * factor),
            (byte)(rgba.G * factor),
            (byte)(rgba.B * factor),
            rgba.A);
    }

    private static void DrawCell(Image<Rgba32> image, float x, float y, float size, Color color, bool use3D, int count)
    {
        if (use3D)
        {
            float depth = 3;
            if (count > 0)
            {
                // Draw depth for non-empty cells
                image.Mutate(ctx => ctx.Fill(Darken(color, 0.6f), new RectangleF(x + depth, y + depth, size, size)));
            }
            image.Mutate(ctx => ctx.Fill(color, new RectangleF(x, y, size, size)));
            if (count > 0)
            {
                // Add a small highlight on top/left
                image.Mutate(ctx => ctx.Fill(Color.FromRgba(255, 255, 255, 40), new RectangleF(x, y, size, 1)));
                image.Mutate(ctx => ctx.Fill(Color.FromRgba(255, 255, 255, 40), new RectangleF(x, y, 1, size)));
            }
        }
        else
        {
            image.Mutate(ctx => ctx.Fill(color, new RectangleF(x, y, size, size)));
        }
    }

    private static void DrawCommonItems(Image<Rgba32> image, List<int> years, List<string> userEmails, 
        Font? fontTitle, Font? fontLabel, int width, int height, bool includePrs, ColorScheme colorScheme, bool use3D = false, string? customTitle = null)
    {
        if (fontTitle != null)
        {
            string titleDisplay = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : string.Join(", ", userEmails);
            string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
            
            image.Mutate(x => x.DrawText(titleDisplay, fontTitle, colorScheme.TextColor, new PointF(Padding, Padding)));
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
                int count = i * 3;
                DrawCell(image, legendX, legendY + 2, CellSize, colorScheme.GetColorForCount(count), use3D, count);
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
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, HeatmapLayout layout, bool includePrs, ColorScheme colorScheme, bool use3D, string? customTitle, bool hideStats)
    {
        int maxWeeks = years.Max(y => CalculateWeeksForYear(y));
        int totalWeeks = years.Sum(y => CalculateWeeksForYear(y));
        int yearSectionHeight = MonthLabelHeight + YearGridHeight + YearGap;
        int yearGapX = 40;
        int horizontalTitleArea = TitleAreaHeight + 20;

        int width, height;
        int statsHeight = hideStats ? 0 : 200;
        if (layout == HeatmapLayout.Horizontal)
        {
            width = Padding + LabelAreaWidth + totalWeeks * Step + (years.Count - 1) * yearGapX + Padding;
            if (!hideStats) width = Math.Max(width, Padding + 750 + Padding);
            height = Padding + horizontalTitleArea + statsHeight + MonthLabelHeight + YearGridHeight + LegendAreaHeight + Padding;
        }
        else
        {
            width = Padding + LabelAreaWidth + maxWeeks * Step + Padding;
            if (!hideStats) width = Math.Max(width, Padding + 750 + Padding);
            height = Padding + TitleAreaHeight + statsHeight + years.Count * yearSectionHeight - YearGap + LegendAreaHeight + Padding;
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
        string titleDisplay = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : string.Join(", ", userEmails);
        titleDisplay = System.Net.WebUtility.HtmlEncode(titleDisplay);
        string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{Padding + 20}\" class=\"title\">{titleDisplay}</text>");
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{Padding + 55}\" class=\"subtitle\">{yearDisplay}</text>");

        if (!hideStats)
        {
            var stats = DashboardStats.Calculate(commitCounts, years);
            float statsTop = Padding + TitleAreaHeight - 10;
            float currentX = Padding;
            float boxWidth = 150;
            float rowHeight = 70;
            
            // Row 1
            DrawSvgStat(writer, "Total Commits", stats.TotalCommits.ToString(), currentX, statsTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Active Days", stats.ActiveDays.ToString(), currentX, statsTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Max per Day", stats.MaxCommitsPerDay.ToString(), currentX, statsTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Avg per Active Day", stats.AverageCommitsPerActiveDay.ToString("F1"), currentX, statsTop, subtextHex, textHex);

            // Row 2
            currentX = Padding;
            float secondRowTop = statsTop + rowHeight;
            DrawSvgStat(writer, "Longest Streak", $"{stats.LongestStreak} days", currentX, secondRowTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Most Active Day", stats.MostActiveDayOfWeek.ToString(), currentX, secondRowTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Most Active Month", stats.MostActiveMonth, currentX, secondRowTop, subtextHex, textHex);
        }

        float gridLeftBase = Padding + LabelAreaWidth;
        float gridTopBase = layout == HeatmapLayout.Horizontal ? Padding + horizontalTitleArea + statsHeight
            : Padding + TitleAreaHeight + statsHeight;

        var sortedYears = layout == HeatmapLayout.Horizontal 
            ? years.OrderBy(y => y).ToList() 
            : years.OrderByDescending(y => y).ToList();

        float currentGridLeft = gridLeftBase;
        for (int yi = 0; yi < sortedYears.Count; yi++)
        {
            int year = sortedYears[yi];
            float sectionTop = layout == HeatmapLayout.Horizontal ? gridTopBase : gridTopBase + yi * yearSectionHeight;
            float gridLeft = layout == HeatmapLayout.Horizontal ? currentGridLeft : gridLeftBase;
            
            DrawSvgYear(writer, year, sectionTop, gridLeft, commitCounts, layout == HeatmapLayout.Horizontal, yi == 0, colorScheme, use3D);
            
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
            int count = i * 3;
            Color c = colorScheme.GetColorForCount(count);
            DrawSvgCell(writer, legendX, legendY, CellSize, c, use3D, count);
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

    private static void DrawSvgStat(StringWriter writer, string label, string value, float x, float y, string subtextHex, string textHex)
    {
        writer.WriteLine($"  <text x=\"{x}\" y=\"{y}\" class=\"label\">{label}</text>");
        writer.WriteLine($"  <text x=\"{x}\" y=\"{y + 25}\" class=\"title\">{value}</text>");
    }

    private static void DrawSvgYear(StringWriter writer, int year, float sectionTop, float gridLeft, 
        Dictionary<DateTime, int> commitCounts, bool isHorizontal, bool isFirstYearInHorizontal, ColorScheme colorScheme, bool use3D)
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
            DrawSvgCell(writer, gridLeft + weekIndex * Step, gridTop + dayOfWeek * Step, CellSize, c, use3D, count);
        }
    }

    private static void DrawSvgCell(StringWriter writer, float x, float y, float size, Color color, bool use3D, int count)
    {
        var rgba = color.ToPixel<Rgba32>();
        string hex = $"#{rgba.R:X2}{rgba.G:X2}{rgba.B:X2}";

        if (use3D)
        {
            float depth = 2;
            if (count > 0)
            {
                // Shadow/Depth
                string shadowHex = $"#{(int)(rgba.R * 0.6):X2}{(int)(rgba.G * 0.6):X2}{(int)(rgba.B * 0.6):X2}";
                writer.WriteLine($"  <rect x=\"{x + depth}\" y=\"{y + depth}\" width=\"{size}\" height=\"{size}\" fill=\"{shadowHex}\" rx=\"2\" ry=\"2\" />");
            }
            writer.WriteLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{size}\" height=\"{size}\" fill=\"{hex}\" rx=\"2\" ry=\"2\" />");
            if (count > 0)
            {
                // Highlight
                writer.WriteLine($"  <line x1=\"{x}\" y1=\"{y}\" x2=\"{x + size}\" y2=\"{y}\" stroke=\"white\" stroke-opacity=\"0.2\" stroke-width=\"1\" />");
                writer.WriteLine($"  <line x1=\"{x}\" y1=\"{y}\" x2=\"{x}\" y2=\"{y + size}\" stroke=\"white\" stroke-opacity=\"0.2\" stroke-width=\"1\" />");
            }
        }
        else
        {
            writer.WriteLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{size}\" height=\"{size}\" fill=\"{hex}\" rx=\"2\" ry=\"2\" />");
        }
    }

    private static string Generate3DSvgChart(List<int> years, List<string> userEmails,
        Dictionary<DateTime, int> commitCounts, string outputPathOrFolder, bool includePrs, ColorScheme colorScheme, string? customTitle, bool hideStats)
    {
        // Isometric Constants
        const float isoW = 14;
        const float isoH = 7;
        const float barScale = 6;
        const float minBarHeight = 3;
        const float cellGap = 1.5f;

        int gridWidth = (int)(58 * isoW);
        int gridHeight = (int)(58 * isoH);

        int statsHeight = hideStats ? 0 : 200;
        int yearStepY = 160; // How much each year adds to total height (vertical overlap for compactness)
        int width = gridWidth + Padding * 2 + 120;
        if (!hideStats) width = Math.Max(width, Padding + 750 + Padding);
        int height = Padding + TitleAreaHeight + statsHeight + (years.Count - 1) * yearStepY + gridHeight + 120 + Padding;

        var sortedYears = years.OrderByDescending(y => y).ToList(); // Draw newest first (back-to-front)
        
        using var writer = new StringWriter();
        string bgHex = colorScheme.BackgroundColor.ToPixel<Rgba32>().ToHex().Substring(0, 6);
        string textHex = colorScheme.TextColor.ToPixel<Rgba32>().ToHex().Substring(0, 6);
        string subtextHex = colorScheme.SubtextColor.ToPixel<Rgba32>().ToHex().Substring(0, 6);

        writer.WriteLine($"<svg width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");
        writer.WriteLine($"  <rect width=\"100%\" height=\"100%\" fill=\"#{bgHex}\" />");

        writer.WriteLine("  <style>");
        writer.WriteLine($"    .title {{ fill: #{textHex}; font-family: Arial, Helvetica, sans-serif; font-size: 20px; font-weight: bold; }}");
        writer.WriteLine($"    .subtitle {{ fill: #{subtextHex}; font-family: Arial, Helvetica, sans-serif; font-size: 20px; font-weight: bold; }}");
        writer.WriteLine($"    .year-label {{ fill: #{textHex}; font-family: Arial, Helvetica, sans-serif; font-size: 14px; font-weight: bold; }}");
        writer.WriteLine($"    .label {{ fill: #{subtextHex}; font-family: Arial, Helvetica, sans-serif; font-size: 12px; }}");
        writer.WriteLine("  </style>");

        string titleDisplay = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : string.Join(", ", userEmails);
        titleDisplay = System.Net.WebUtility.HtmlEncode(titleDisplay);
        string yearDisplay = years.Count == 1 ? (years[0] == 0 ? "All years" : years[0].ToString()) : $"{years.Min()}-{years.Max()}";
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{Padding + 20}\" class=\"title\">{titleDisplay}</text>");
        writer.WriteLine($"  <text x=\"{Padding}\" y=\"{Padding + 55}\" class=\"subtitle\">{yearDisplay}</text>");

        if (!hideStats)
        {
            var stats = DashboardStats.Calculate(commitCounts, years);
            float statsTop = Padding + TitleAreaHeight - 10;
            float currentX = Padding;
            float boxWidth = 150;
            float rowHeight = 70;
            
            // Row 1
            DrawSvgStat(writer, "Total Commits", stats.TotalCommits.ToString(), currentX, statsTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Active Days", stats.ActiveDays.ToString(), currentX, statsTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Max per Day", stats.MaxCommitsPerDay.ToString(), currentX, statsTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Avg per Active Day", stats.AverageCommitsPerActiveDay.ToString("F1"), currentX, statsTop, subtextHex, textHex);

            // Row 2
            currentX = Padding;
            float secondRowTop = statsTop + rowHeight;
            DrawSvgStat(writer, "Longest Streak", $"{stats.LongestStreak} days", currentX, secondRowTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Most Active Day", stats.MostActiveDayOfWeek.ToString(), currentX, secondRowTop, subtextHex, textHex);
            currentX += boxWidth;
            DrawSvgStat(writer, "Most Active Month", stats.MostActiveMonth, currentX, secondRowTop, subtextHex, textHex);
        }

        float originX = Padding + 100 + 6 * isoW; // Correct for min cx offset
        float currentOriginY = Padding + TitleAreaHeight + statsHeight + 60;

        foreach (var year in sortedYears)
        {
            writer.WriteLine($"  <text x=\"{Padding}\" y=\"{currentOriginY + 34}\" class=\"year-label\">{year}</text>");

            DateTime startDate = new DateTime(year, 1, 1);
            int totalDays = (new DateTime(year, 12, 31) - startDate).Days + 1;
            int startOffset = (int)startDate.DayOfWeek;

            // Track month labels
            int currentMonth = 0;
            var monthLabels = new List<(string Text, float X, float Y)>();

            for (int i = 0; i < totalDays; i++)
            {
                DateTime currentDate = startDate.AddDays(i);
                int week = (i + startOffset) / 7;
                int day = (int)currentDate.DayOfWeek;

                // Track month labels for isometric view
                if (currentDate.Month != currentMonth)
                {
                    currentMonth = currentDate.Month;
                    string monthStr = currentDate.ToString("MMM");
                    float monthX = originX + week * isoW;
                    float monthY = currentOriginY + week * isoH - 35;
                    monthLabels.Add((monthStr, monthX, monthY));
                }

                float cx = originX + (week - day) * isoW;
                float cy = currentOriginY + (week + day) * isoH;

                int count = commitCounts.GetValueOrDefault(currentDate, 0);
                Color baseColor = colorScheme.GetColorForCount(count);
                float barHeight = count == 0 ? 2 : Math.Min(40, count) * barScale / 2.5f + minBarHeight;

                DrawSvgIsometricBar(writer, cx, cy, isoW - cellGap, isoH - cellGap, barHeight, baseColor);
            }

            // Draw month labels after bars to ensure they are on top
            foreach (var label in monthLabels)
            {
                writer.WriteLine($"  <text x=\"{label.X}\" y=\"{label.Y + 12}\" class=\"label\">{label.Text}</text>");
            }

            currentOriginY += yearStepY;
        }

        writer.WriteLine("</svg>");

        string defaultFileName = GetDefaultFileName(years, HeatmapLayout.Vertical, OutputFormat.Svg);
        string finalPath = ResolveOutputPath(outputPathOrFolder, defaultFileName);
        
        string? dir = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        File.WriteAllText(finalPath, writer.ToString());
        return finalPath;
    }

    private static void DrawSvgIsometricBar(StringWriter writer, float x, float y, float w, float h, float height, Color color)
    {
        var rgba = color.ToPixel<Rgba32>();
        string topHex = $"#{rgba.R:X2}{rgba.G:X2}{rgba.B:X2}";
        
        // Define faces
        // Top Face
        string topPoints = $"{x},{y - height} {x + w},{y + h - height} {x},{y + 2 * h - height} {x - w},{y + h - height}";
        
        // Darken left/right
        string leftHex = $"#{(byte)(rgba.R * 0.85):X2}{(byte)(rgba.G * 0.85):X2}{(byte)(rgba.B * 0.85):X2}";
        string rightHex = $"#{(byte)(rgba.R * 0.70):X2}{(byte)(rgba.G * 0.70):X2}{(byte)(rgba.B * 0.70):X2}";
        
        // Left Face
        string leftPoints = $"{x - w},{y + h - height} {x},{y + 2 * h - height} {x},{y + 2 * h} {x - w},{y + h}";
        
        // Right Face
        string rightPoints = $"{x},{y + 2 * h - height} {x + w},{y + h - height} {x + w},{y + h} {x},{y + 2 * h}";

        writer.WriteLine($"  <polygon points=\"{leftPoints}\" fill=\"{leftHex}\" />");
        writer.WriteLine($"  <polygon points=\"{rightPoints}\" fill=\"{rightHex}\" />");
        writer.WriteLine($"  <polygon points=\"{topPoints}\" fill=\"{topHex}\" stroke=\"white\" stroke-opacity=\"0.06\" stroke-width=\"0.5\" />");
    }
}
