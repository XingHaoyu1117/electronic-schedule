using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElectronicSchedule;

/// <summary>界面外观设置，对应 schedule.json 里的 appearance 节。</summary>
public sealed class AppearanceOptions
{
    /// <summary>整窗不透明度，0.3 ~ 1.0。</summary>
    public double Opacity { get; set; } = 0.9;

    /// <summary>展开/收起课表时是否播放高度动画。</summary>
    public bool AnimateExpand { get; set; } = true;
}

/// <summary>上下课提醒，对应 schedule.json 里的 reminders 节。</summary>
public sealed class ReminderOptions
{
    /// <summary>总开关，关闭后不响铃也不提示。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>预备铃提前多少分钟，0 表示不打预备铃。</summary>
    public int PrepareMinutes { get; set; } = 2;

    /// <summary>是否播放预备铃。</summary>
    public bool Prepare { get; set; } = true;

    /// <summary>是否播放上课铃。</summary>
    public bool Start { get; set; } = true;

    /// <summary>是否播放下课铃。</summary>
    public bool End { get; set; } = true;

    /// <summary>提醒在横条上停留多少秒。</summary>
    public int BannerSeconds { get; set; } = 8;
}

public sealed class Lesson
{
    /// <summary>1 = 周一 … 7 = 周日。</summary>
    public int Day { get; set; }

    /// <summary>形如 "08:00"。</summary>
    public string Start { get; set; } = "";

    /// <summary>形如 "08:45"。结束时间不含在本节课内。</summary>
    public string End { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>只用于临时调课：该条覆盖的具体日期，形如 "2026-10-30"。</summary>
    public string? Date { get; set; }

    [JsonIgnore]
    public TimeSpan StartTime => ParseTime(Start);

    [JsonIgnore]
    public TimeSpan EndTime => ParseTime(End);

    private static TimeSpan ParseTime(string value) =>
        TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time) ? time : TimeSpan.Zero;
}

public sealed class Schedule
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Title { get; set; } = "电子课表";

    [JsonPropertyName("appearance")]
    public AppearanceOptions Appearance { get; set; } = new();

    /// <summary>上下课提醒设置。</summary>
    [JsonPropertyName("reminders")]
    public ReminderOptions Reminders { get; set; } = new();

    public List<Lesson> Lessons { get; set; } = new();

    /// <summary>
    /// 临时调课，只对指定日期生效，优先级高于常规课表。
    /// 同一格子（同学日、同时间段）写了调课就以调课为准，课名为空表示当天该节课取消。
    /// </summary>
    [JsonPropertyName("overrides")]
    public List<Lesson> Overrides { get; set; } = new();

    /// <summary>与可执行文件同目录的 schedule.json。</summary>
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "schedule.json");

    /// <summary>把当前对象写回 schedule.json，中文保持可读。</summary>
    public void Save()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, options), new UTF8Encoding(false));
    }

    public static Schedule LoadOrDefault()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                // 用 UTF-8 显式读，避免被 PowerShell 等工具写成带 BOM 或本地编码后读成乱码。
                var schedule = JsonSerializer.Deserialize<Schedule>(File.ReadAllText(FilePath, Encoding.UTF8), Options);
                if (schedule is not null && schedule.Lessons.Count > 0)
                    return schedule;
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }

        return Sample();
    }

    public static Schedule Sample() => new()
    {
        Lessons =
        {
            new() { Day = 1, Start = "08:00", End = "08:45", Name = "语文" },
            new() { Day = 1, Start = "08:55", End = "09:40", Name = "数学" },
            new() { Day = 1, Start = "09:50", End = "10:35", Name = "英语" },
            new() { Day = 1, Start = "10:45", End = "11:30", Name = "物理" },
            new() { Day = 1, Start = "14:00", End = "14:45", Name = "化学" },
            new() { Day = 1, Start = "14:55", End = "15:40", Name = "体育" },
            new() { Day = 1, Start = "15:50", End = "16:35", Name = "历史" }
        }
    };

    public List<Lesson> ForDay(DateTime now)
    {
        int day = DayNumber(now.DayOfWeek);
        string date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var lessons = Lessons.Where(x => x.Day == day).ToList();

        foreach (var adjusted in Overrides.Where(o => o.Date == date))
        {
            // 调课按"同学日 + 同时间段"覆盖原课：先删掉原格子，再决定是否放回。
            lessons.RemoveAll(x => x.StartTime == adjusted.StartTime && x.EndTime == adjusted.EndTime);

            if (!string.IsNullOrWhiteSpace(adjusted.Name))
            {
                lessons.Add(new Lesson
                {
                    Day = adjusted.Day,
                    Start = adjusted.Start,
                    End = adjusted.End,
                    Name = adjusted.Name
                });
            }
        }

        return lessons.OrderBy(x => x.StartTime).ToList();
    }

    /// <summary>指定日期上是否配置了调课，界面上用来提示。</summary>
    public bool HasOverrides(DateTime now)
    {
        string date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Overrides.Any(o => o.Date == date);
    }

    public Lesson? Current(DateTime now)
    {
        var time = now.TimeOfDay;

        // 与 ForDay 同源，但只找"当前这一节"，不排序也不新建列表。
        int day = DayNumber(now.DayOfWeek);
        string date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var overrides = Overrides.Where(o => o.Date == date).ToList();

        foreach (var lesson in Lessons)
        {
            if (lesson.Day != day)
                continue;

            if (time < lesson.StartTime || time >= lesson.EndTime)
                continue;

            // 这一节若被调课覆盖，就以调课结果为准（课名为空表示取消）。
            var adjusted = overrides.FirstOrDefault(o => o.StartTime == lesson.StartTime);
            if (adjusted is not null)
                return adjusted.Name.Length == 0 ? null : adjusted;

            return lesson;
        }

        // 原课表里没课，但调课加了一节。
        foreach (var adjusted in overrides)
        {
            if (adjusted.Name.Length > 0 && time >= adjusted.StartTime && time < adjusted.EndTime)
                return adjusted;
        }

        return null;
    }

    private static int DayNumber(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
}
