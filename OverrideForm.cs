using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ElectronicSchedule;

/// <summary>临时调课：为某个具体日期覆盖当天的若干节课，有效期到那天为止。</summary>
public sealed class OverrideForm : Form
{
    private readonly Schedule schedule;
    private readonly DataGridView grid = new();
    private readonly DateTimePicker datePicker = new();
    private readonly Label hintLabel = new();

    /// <summary>关闭时是否有改动。true 表示 schedule.json 已被写盘。</summary>
    public bool Changed { get; private set; }

    public OverrideForm(Schedule current)
    {
        schedule = Clone(current);

        Text = "临时调课";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(860, 520);
        MinimumSize = new Size(720, 420);
        Font = new Font("Microsoft YaHei UI", 9.5f);
        ShowInTaskbar = false;

        Controls.Add(BuildGridHost());
        Controls.Add(BuildFooter());
        Controls.Add(BuildToolbar());

        datePicker.Value = DateTime.Today;
        LoadDay(datePicker.Value);
    }

    private Control BuildToolbar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(10, 12, 10, 6),
            WrapContents = false,
            BackColor = Color.FromArgb(246, 247, 249)
        };

        bar.Controls.Add(new Label { Text = "调课日期", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });

        datePicker.Format = DateTimePickerFormat.Custom;
        datePicker.CustomFormat = "yyyy年M月d日 dddd";
        datePicker.Width = 200;
        datePicker.Margin = new Padding(0, 4, 14, 0);
        datePicker.ValueChanged += (_, _) => LoadDay(datePicker.Value);
        bar.Controls.Add(datePicker);

        bar.Controls.Add(MakeButton("写回原课表", (_, _) => RestoreDay(), false));
        bar.Controls.Add(MakeButton("清除该日调课", (_, _) => ClearDay(), false));
        bar.Controls.Add(MakeButton("保存", (_, _) => SaveAll(), true));

        return bar;
    }

    private Control BuildGridHost()
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.RowTemplate.Height = 34;
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "节点",
            FillWeight = 22,
            MinimumWidth = 130,
            ReadOnly = true
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "内容",
            FillWeight = 78,
            MinimumWidth = 200
        });

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
        host.Controls.Add(grid);
        return host;
    }

    private Control BuildFooter()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(14, 12, 14, 8),
            BackColor = Color.FromArgb(246, 247, 249)
        };

        bar.Controls.Add(hintLabel);
        hintLabel.Dock = DockStyle.Fill;
        hintLabel.AutoSize = false;
        hintLabel.ForeColor = Color.FromArgb(110, 116, 126);
        hintLabel.Text = "把要改的那节课写成新的课程名；清空表示当天取消这节课；未改动的格子保持原样。";

        return bar;
    }

    private static Button MakeButton(string text, EventHandler click, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(primary ? 90 : 116, 30),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(35, 125, 90) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(40, 44, 50)
        };

        button.FlatAppearance.BorderColor = Color.FromArgb(206, 212, 220);
        button.Click += click;
        return button;
    }

    private string DateKey(DateTime day) => day.ToString("yyyy-MM-dd");

    private string WeekInfo(DateTime day) => "第" + System.Globalization.ISOWeek.GetWeekOfYear(day) + "周";

    /// <summary>当天在该日期的有效课表（已含调课结果）。</summary>
    private List<Lesson> EffectiveLessons(DateTime day)
    {
        var date = DateKey(day);

        // 用一份只含常规课表的克隆算出原始内容，避免把调课结果当成原课表。
        var baseOnly = Clone(schedule);
        baseOnly.Overrides.Clear();
        return baseOnly.ForDay(day);
    }

    private void LoadDay(DateTime day)
    {
        grid.Rows.Clear();

        var date = DateKey(day);
        var effective = schedule.ForDay(day);
        var baseOnly = EffectiveLessons(day);
        var overrides = schedule.Overrides.Where(o => o.Date == date).ToList();

        // 以常规课表为基础列出所有格子，再加调课引入的新时间段。
        var periods = baseOnly
            .Select(l => (l.StartTime, l.EndTime))
            .Union(overrides.Select(o => (o.StartTime, o.EndTime)))
            .Distinct()
            .OrderBy(p => p.Item1)
            .ToList();

        foreach (var (start, end) in periods)
        {
            var origin = baseOnly.FirstOrDefault(l => l.StartTime == start && l.EndTime == end);
            var now = effective.FirstOrDefault(l => l.StartTime == start && l.EndTime == end);

            string label = $"{FormatTime(start)}-{FormatTime(end)}";
            string text;
            bool changed;

            if (origin is null)
            {
                // 原本没有这节课，是调课加进来的。
                text = now?.Name ?? string.Empty;
                changed = true;
            }
            else if (now is null)
            {
                // 调课把它取消了。
                text = string.Empty;
                changed = true;
            }
            else
            {
                text = now.Name;
                changed = now.Name != origin.Name;
            }

            int index = grid.Rows.Add(label, text);
            var cell = grid.Rows[index].Cells[1];

            if (changed)
            {
                cell.Style.BackColor = Color.FromArgb(255, 244, 214);
                cell.Style.ForeColor = Color.FromArgb(120, 84, 0);
            }

            cell.Tag = new CellContext(origin, now);
        }

        if (periods.Count == 0)
            grid.Rows.Add("当天没有课程", string.Empty);

        grid.Columns[0].HeaderText = $"{WeekInfo(day)} 的节点";
    }

    private void RestoreDay()
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Cells[1].Tag is not CellContext context)
                continue;

            grid.Rows[i].Cells[1].Value = context.Origin?.Name ?? string.Empty;
            grid.Rows[i].Cells[1].Style.BackColor = Color.White;
            grid.Rows[i].Cells[1].Style.ForeColor = Color.Black;
        }
    }

    private void ClearDay()
    {
        string date = DateKey(datePicker.Value);
        var before = schedule.Overrides.Count;
        schedule.Overrides.RemoveAll(o => o.Date == date);

        if (schedule.Overrides.Count != before)
            hint("已清除该日全部调课，点「保存」生效。");

        LoadDay(datePicker.Value);
    }

    private void hint(string text)
    {
        hintLabel.Text = text;
    }

    private void SaveAll()
    {
        string date = DateKey(datePicker.Value);
        var day = datePicker.Value;

        // 只保留用户确实改动过的格子，其余交给常规课表。
        // 调课必须落在"该日期所在的那一天"，所以 Day 用 day 的星期。
        var kept = schedule.Overrides.Where(o => o.Date != date).ToList();

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.Cells[0].Value is not string label || row.Cells[1].Tag is not CellContext context)
                continue;

            string text = row.Cells[1].Value?.ToString()?.Trim() ?? string.Empty;
            string original = context.Origin?.Name ?? string.Empty;

            if (text == original)
                continue;

            if (!TryParseRange(label, out var start, out var end))
                continue;

            kept.Add(new Lesson
            {
                Day = DayNumber(day.DayOfWeek),
                Start = FormatTime(start),
                End = FormatTime(end),
                Name = text,
                Date = date
            });
        }

        schedule.Overrides = kept;

        try
        {
            schedule.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：" + ex.Message, "临时调课", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Changed = true;
        Close();
    }

    private static int DayNumber(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;

    private static string FormatTime(TimeSpan time) => $"{time.Hours:00}:{time.Minutes:00}";

    private static bool TryParseRange(string text, out TimeSpan start, out TimeSpan end)
    {
        start = TimeSpan.Zero;
        end = TimeSpan.Zero;

        var parts = text.Split('-');
        if (parts.Length != 2)
            return false;

        if (!TimeSpan.TryParseExact(parts[0].Trim(), @"hh\:mm", null, out start))
            return false;

        if (!TimeSpan.TryParseExact(parts[1].Trim(), @"hh\:mm", null, out end))
            return false;

        return end > start;
    }

    /// <summary>每个格子记住：这节课在原课表里是什么、调课之后是什么。</summary>
    private sealed record CellContext(Lesson? Origin, Lesson? Effective);

    /// <summary>深拷贝，防止编辑时改到全局对象。</summary>
    private static Schedule Clone(Schedule source) => new()
    {
        Title = source.Title,
        Appearance = new AppearanceOptions { Opacity = source.Appearance.Opacity },
        Lessons = source.Lessons
            .Select(l => new Lesson { Day = l.Day, Start = l.Start, End = l.End, Name = l.Name })
            .ToList(),
        Overrides = source.Overrides
            .Select(o => new Lesson
            {
                Day = o.Day, Start = o.Start, End = o.End, Name = o.Name, Date = o.Date
            })
            .ToList()
    };
}