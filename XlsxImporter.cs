using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ElectronicSchedule;

/// <summary>把 xlsx 课表转换成课程列表：识别「周一…周日」表头，每行第一个能解析出时间段的单元格作为节次。</summary>
internal static class XlsxImporter
{
    private sealed record Period(TimeSpan Start, TimeSpan End);

    public static List<Lesson>? Import(string path, out string message)
    {
        List<List<string>> rows;

        try
        {
            rows = XlsxReader.ReadFirstSheet(path);
        }
        catch (Exception ex)
        {
            message = "读取失败：" + ex.Message;
            return null;
        }

        int headerRow = -1;
        var dayColumns = new int[8];

        for (int i = 0; i < rows.Count && headerRow < 0; i++)
        {
            var found = new int[8];
            int count = 0;

            for (int c = 0; c < rows[i].Count; c++)
            {
                int day = DayFromHeader(rows[i][c]);
                if (day > 0 && found[day] == 0)
                {
                    found[day] = c + 1;
                    count++;
                }
            }

            if (count >= 3)
            {
                headerRow = i;
                dayColumns = found;
            }
        }

        if (headerRow < 0)
        {
            message = "没找到「周一」到「周日」的表头。\n请在表格第一行写上星期，每列一天。";
            return null;
        }

        var lessons = new List<Lesson>();
        int periods = 0;

        for (int i = headerRow + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            Period? period = null;

            // 时间一般在最左侧几列，找到第一个能解析的就当节次。
            for (int c = 0; c < Math.Min(row.Count, 4); c++)
            {
                if (TryParseRange(row[c], out var start, out var end))
                {
                    period = new Period(start, end);
                    break;
                }
            }

            if (period is null)
                continue;

            periods++;

            for (int day = 1; day <= 7; day++)
            {
                int column = dayColumns[day];
                if (column <= 0 || column > row.Count)
                    continue;

                string name = CleanName(row[column - 1]);

                if (name.Length == 0)
                    continue;

                lessons.Add(new Lesson
                {
                    Day = day,
                    Start = $"{period.Start.Hours:00}:{period.Start.Minutes:00}",
                    End = $"{period.End.Hours:00}:{period.End.Minutes:00}",
                    Name = name
                });
            }
        }

        if (lessons.Count == 0)
        {
            message = "表头下面没有读到课程。请检查时间和课程名是否写在一起了。";
            return null;
        }

        message = $"读取 {periods} 个节次、{lessons.Count} 节课";
        return lessons;
    }

    /// <summary>去掉旧版单双周后缀，避免导入后课程名里带着「(单)」。</summary>
    private static string CleanName(string text)
    {
        text = text.Trim();

        foreach (var suffix in new[] { "(单)", "（单）", "(双)", "（双）", "单周", "双周" })
        {
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return text[..^suffix.Length].Trim();
        }

        return text;
    }

    private static int DayFromHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        for (int day = 1; day <= 7; day++)
        {
            char c = "一二三四五六日"[day - 1];
            if (text.Contains("周" + c) || text.Contains("星期" + c))
                return day;
        }

        return 0;
    }

    private static bool TryParseRange(string text, out TimeSpan start, out TimeSpan end)
    {
        start = TimeSpan.Zero;
        end = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(\d{1,2})[:：](\d{2})\s*[-–—~至到]\s*(\d{1,2})[:：](\d{2})");

        if (!match.Success)
            return false;

        start = new TimeSpan(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), 0);
        end = new TimeSpan(int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value), 0);

        return end > start;
    }
}
