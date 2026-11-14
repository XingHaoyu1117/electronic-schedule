using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ElectronicSchedule;

/// <summary>
/// 可视化课表编辑器。
/// 默认视图是"按天编辑"：左边是周一到周日，右边是当天一节课一节课地加，
/// 每节课一行，填课程名、选次数（每周/单周/双周）就行，不需要理解 JSON 或后缀语法。
/// </summary>
public sealed class EditorForm : Form
{
    private static readonly string[] DayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

    private static readonly Regex TimeRange = new(
        @"(\d{1,2})[:：](\d{2})\s*[-–—~至到]\s*(\d{1,2})[:：](\d{2})",
        RegexOptions.Compiled);

    private readonly Schedule working;
    private readonly ListBox dayList = new();
    private readonly FlowLayoutPanel lessonHost = new();
    private readonly TextBox titleBox = new();
    private readonly NumericUpDown opacityBox = new();
    private readonly Label statusLabel = new();
    private readonly Label emptyHint = new();

    private int selectedDay = 1;

    public bool Saved { get; private set; }

    public EditorForm()
    {
        working = Schedule.LoadOrDefault();

        Text = "课表设置";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(760, 500);
        Font = new Font("Microsoft YaHei UI", 10f);
        ShowInTaskbar = true;

        Controls.Add(BuildBody());
        Controls.Add(BuildFooter());
        Controls.Add(BuildHeader());

        LoadDay(DayNumber(DateTime.Today.DayOfWeek));

        var conflictDays = Enumerable.Range(1, 7)
            .Where(day => HasOverlaps(working.Lessons.Where(l => l.Day == day)))
            .Select(day => DayNames[day - 1])
            .ToList();

        if (conflictDays.Count > 0)
            statusLabel.Text = "现有课表有时间重叠：" + string.Join("、", conflictDays) + "；请检查";
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(16, 0, 16, 0),
            BackColor = Color.White
        };

        var title = new Label
        {
            Text = "课表设置",
            Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var tip = new Label
        {
            Text = "左边选星期，右边一行填一节课。改完点右下角「保存」，横条会立刻更新。",
            AutoSize = true,
            ForeColor = Color.FromArgb(120, 126, 136),
            Location = new Point(130, 20)
        };

        panel.Controls.Add(title);
        panel.Controls.Add(tip);
        return panel;
    }

    private Control BuildBody()
    {
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 0) };

        // 左侧：星期选择
        var dayBox = new GroupBox
        {
            Text = " 选择星期 ",
            Dock = DockStyle.Left,
            Width = 170,
            Padding = new Padding(12, 6, 12, 8),
            ForeColor = Color.FromArgb(90, 96, 106)
        };

        dayList.Dock = DockStyle.Fill;
        dayList.BorderStyle = BorderStyle.None;
        dayList.Font = new Font("Microsoft YaHei UI", 11.5f);
        dayList.DrawMode = DrawMode.OwnerDrawFixed;
        dayList.ItemHeight = 40;
        dayList.IntegralHeight = false;
        dayList.DrawItem += DayListDrawItem;
        dayList.Items.AddRange(DayNames);
        dayList.SelectedIndexChanged += (_, _) =>
        {
            if (dayList.SelectedIndex >= 0)
                LoadDay(dayList.SelectedIndex + 1);
        };

        dayBox.Controls.Add(dayList);

        // 右侧：当天的课程
        var lessonBox = new GroupBox
        {
            Text = " 当天课程 ",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 10),
            ForeColor = Color.FromArgb(90, 96, 106)
        };

        lessonHost.Dock = DockStyle.Fill;
        lessonHost.FlowDirection = FlowDirection.TopDown;
        lessonHost.WrapContents = false;
        lessonHost.AutoScroll = true;
        lessonHost.BackColor = Color.White;

        emptyHint.Text = "这一天还没有课。点下面的「添加一节课」开始。";
        emptyHint.AutoSize = true;
        emptyHint.ForeColor = Color.Gray;
        emptyHint.Margin = new Padding(6, 12, 0, 0);
        lessonHost.Controls.Add(emptyHint);

        lessonBox.Controls.Add(lessonHost);

        var spacer = new Panel { Dock = DockStyle.Left, Width = 10 };

        body.Controls.Add(lessonBox);
        body.Controls.Add(spacer);
        body.Controls.Add(dayBox);
        return body;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 74,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        var add = new Button
        {
            Text = "＋ 添加一节课",
            Size = new Size(150, 36),
            Location = new Point(16, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        add.FlatAppearance.BorderColor = Color.FromArgb(200, 206, 214);
        add.Click += (_, _) => AddLessonRow(null);

        var import = new Button
        {
            Text = "从 Excel 导入…",
            Size = new Size(140, 36),
            Location = new Point(176, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        import.FlatAppearance.BorderColor = Color.FromArgb(200, 206, 214);
        import.Click += (_, _) => ImportXlsx();

        var overrideButton = new Button
        {
            Text = "临时调课…",
            Size = new Size(120, 36),
            Location = new Point(326, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
        overrideButton.FlatAppearance.BorderColor = Color.FromArgb(200, 206, 214);
        overrideButton.Click += (_, _) => OpenOverrides();

        var save = new Button
        {
            Text = "保存",
            Size = new Size(110, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 142, 18),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(35, 125, 90),
            ForeColor = Color.White
        };
        save.FlatAppearance.BorderColor = Color.FromArgb(35, 125, 90);
        save.Click += (_, _) => SaveSchedule();
        footer.Resize += (_, _) => save.Left = footer.Width - save.Width - 16;

        statusLabel.AutoSize = false;
        statusLabel.Size = new Size(280, 22);
        statusLabel.Location = new Point(460, 26);
        statusLabel.ForeColor = Color.FromArgb(110, 116, 126);
        statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        footer.Controls.Add(add);
        footer.Controls.Add(import);
        footer.Controls.Add(overrideButton);
        footer.Controls.Add(statusLabel);
        footer.Controls.Add(save);

        // 标题和不透明度放在最下面一行
        var extra = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(16, 8, 16, 8),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        var titleLabel = new Label { Text = "横条文字", AutoSize = true, Location = new Point(16, 12) };

        titleBox.Size = new Size(160, 26);
        titleBox.Location = new Point(88, 8);
        titleBox.Text = working.Title;

        var opacityLabel = new Label { Text = "不透明度", AutoSize = true, Location = new Point(264, 12) };

        opacityBox.DecimalPlaces = 2;
        opacityBox.Increment = 0.05m;
        opacityBox.Minimum = 0.3m;
        opacityBox.Maximum = 1.0m;
        opacityBox.Value = (decimal)Math.Clamp(working.Appearance.Opacity, 0.3, 1.0);
        opacityBox.Width = 70;
        opacityBox.Location = new Point(330, 8);
        opacityBox.ValueChanged += (_, _) =>
            working.Appearance.Opacity = (double)opacityBox.Value;

        extra.Controls.Add(titleLabel);
        extra.Controls.Add(titleBox);
        extra.Controls.Add(opacityLabel);
        extra.Controls.Add(opacityBox);

        var stack = new Panel { Dock = DockStyle.Bottom, Height = 118 };
        stack.Controls.Add(extra);
        stack.Controls.Add(footer);
        return stack;
    }

    private static bool HasOverlaps(IEnumerable<Lesson> source)
    {
        var ordered = source.OrderBy(l => l.StartTime).ToList();
        if (ordered.Count < 2)
            return false;

        TimeSpan latestEnd = ordered[0].EndTime;

        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].StartTime < latestEnd)
                return true;

            if (ordered[i].EndTime > latestEnd)
                latestEnd = ordered[i].EndTime;
        }

        return false;
    }

    private int DayNumber(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;

    /// <summary>星期列表自绘：选中项用绿色底，未选中透明，避免系统默认的蓝底方块。</summary>
    private void DayListDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;

        bool selected = (e.State & DrawItemState.Selected) != 0;

        using (var back = new SolidBrush(selected ? Color.FromArgb(35, 125, 90) : Color.White))
            e.Graphics.FillRectangle(back, e.Bounds);

        string text = DayNames[e.Index];
        int count = working.Lessons.Count(l => l.Day == e.Index + 1);
        string suffix = count > 0 ? $"  {count} 节" : string.Empty;

        using var font = new Font(dayList.Font, selected ? FontStyle.Bold : FontStyle.Regular);
        using var fore = new SolidBrush(selected ? Color.White : Color.FromArgb(60, 66, 74));

        e.Graphics.DrawString(text, font, fore, e.Bounds.Left + 8, e.Bounds.Top + 8);

        if (suffix.Length > 0)
        {
            using var small = new Font(dayList.Font.FontFamily, 9f);
            using var dim = new SolidBrush(selected ? Color.FromArgb(220, 245, 232) : Color.Gray);
            var size = e.Graphics.MeasureString(text, font);
            e.Graphics.DrawString(suffix, small, dim, e.Bounds.Left + 8 + size.Width, e.Bounds.Top + 12);
        }
    }

    private void LoadDay(int day)
    {
        selectedDay = day;

        if (dayList.SelectedIndex != day - 1)
            dayList.SelectedIndex = day - 1;

        lessonHost.SuspendLayout();

        foreach (Control control in lessonHost.Controls.Cast<Control>().ToList())
        {
            if (control != emptyHint)
                control.Dispose();
        }

        lessonHost.Controls.Clear();

        var lessons = working.Lessons
            .Where(l => l.Day == day)
            .OrderBy(l => l.StartTime)
            .ToList();

        if (lessons.Count == 0)
        {
            lessonHost.Controls.Add(emptyHint);
        }
        else
        {
            foreach (var lesson in lessons)
                AddLessonRow(lesson, rebuild: true);
        }

        lessonHost.ResumeLayout();
        dayList.Invalidate();
    }

    /// <summary>一行 = 一节课：时间、课程名、周次、删除。</summary>
    private void AddLessonRow(Lesson? source, bool rebuild = false)
    {
        var row = new Panel
        {
            Width = Math.Max(400, lessonHost.ClientSize.Width - 24),
            Height = 50,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.FromArgb(250, 251, 252)
        };

        // 时间
        var start = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 92,
            Location = new Point(6, 10),
            Value = DateTime.Today.Add(source?.StartTime ?? NextDefaultStart())
        };

        var dash = new Label { Text = "至", AutoSize = true, Location = new Point(104, 14) };

        var end = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 92,
            Location = new Point(124, 10),
            Value = DateTime.Today.Add(source?.EndTime ?? NextDefaultStart().Add(TimeSpan.FromMinutes(45)))
        };

        // 课程名
        var name = new TextBox
        {
            Width = 280,
            Location = new Point(232, 12),
            Text = source?.Name ?? string.Empty,
            PlaceholderText = "课程名，如 语文"
        };

        var remove = new Button
        {
            Text = "删除",
            Size = new Size(56, 28),
            Location = new Point(524, 11),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(150, 60, 60)
        };
        remove.FlatAppearance.BorderColor = Color.FromArgb(220, 210, 210);
        remove.Click += (_, _) =>
        {
            row.Tag = null; // 标记为已删除
            row.Dispose();
            lessonHost.PerformLayout();
        };

        row.Controls.Add(start);
        row.Controls.Add(dash);
        row.Controls.Add(end);
        row.Controls.Add(name);
        row.Controls.Add(remove);

        // 用一个包容器承载"起点/终点/名字"，保存时整行读回。
        row.Tag = new object[] { start, end, name };

        lessonHost.Controls.Add(row);
        lessonHost.PerformLayout();

        if (!rebuild && source is null)
            name.Focus();
    }

    private TimeSpan NextDefaultStart()
    {
        var rows = lessonHost.Controls.OfType<Panel>()
            .Select(p => p.Tag as object[])
            .Where(t => t is not null)
            .Select(t => ((DateTimePicker)t![1]).Value.TimeOfDay)
            .ToList();

        if (rows.Count == 0)
            return new TimeSpan(8, 0, 0);

        var lastEnd = rows.Max();
        return lastEnd.Add(TimeSpan.FromMinutes(10));
    }

    private void ImportXlsx()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择课表文件",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var imported = XlsxImporter.Import(dialog.FileName, out string message);
        if (imported is null)
        {
            MessageBox.Show(this, message, "导入 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(this,
                $"已从表格读到 {imported.Count} 节课。\n\n导入会替换当前全部课表，继续吗？",
                "导入 Excel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        working.Lessons = imported;
        LoadDay(selectedDay);
        statusLabel.Text = "已导入，记得点保存";
    }

    private void OpenOverrides()
    {
        using var form = new OverrideForm(working);
        form.ShowDialog(this);

        if (form.Changed)
            statusLabel.Text = "临时调课已保存";
    }

    /// <summary>命令行传入 xlsx 时直接导入，跳过文件选择框。</summary>
    public void StartImport(string path)
    {
        var imported = XlsxImporter.Import(path, out string message);
        if (imported is null)
        {
            MessageBox.Show(this, message, "导入 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        working.Lessons = imported;
        LoadDay(selectedDay);
        statusLabel.Text = $"已导入 {imported.Count} 节课，记得点保存";
    }

    private void SaveSchedule()
    {
        var lessons = new List<Lesson>();

        foreach (var row in lessonHost.Controls.OfType<Panel>())
        {
            if (row.Tag is not object[] fields)
                continue;

            var start = ((DateTimePicker)fields[0]).Value.TimeOfDay;
            var end = ((DateTimePicker)fields[1]).Value.TimeOfDay;
            string name = ((TextBox)fields[2]).Text.Trim();

            if (name.Length == 0)
                continue;

            if (end <= start)
            {
                MessageBox.Show(this, $"「{name}」的下课时间要晚于上课时间。", "保存",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lessons.Add(new Lesson
            {
                Day = selectedDay,
                Start = $"{start.Hours:00}:{start.Minutes:00}",
                End = $"{end.Hours:00}:{end.Minutes:00}",
                Name = name
            });
        }

        // 同一天的课程时间不能重叠，否则当前课程判断和铃声提醒会产生歧义。
        for (int i = 0; i < lessons.Count; i++)
        {
            for (int j = i + 1; j < lessons.Count; j++)
            {
                var first = lessons[i];
                var second = lessons[j];

                if (first.StartTime < second.EndTime && second.StartTime < first.EndTime)
                {
                    MessageBox.Show(this,
                        $"「{first.Name}」({first.Start}-{first.End}) 与「{second.Name}」({second.Start}-{second.End}) 时间重叠，请先调整。",
                        "课程时间冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        // 只替换当前这一天，其它天的课程原样保留。
        var others = working.Lessons.Where(l => l.Day != selectedDay).ToList();
        others.AddRange(lessons);
        working.Lessons = others
            .OrderBy(l => l.Day)
            .ThenBy(l => l.StartTime)
            .ToList();

        working.Title = titleBox.Text.Trim() is { Length: > 0 } title ? title : "电子课表";
        working.Appearance.Opacity = (double)opacityBox.Value;

        try
        {
            working.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败：" + ex.Message, "保存", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        statusLabel.Text = $"已保存 {DayNames[selectedDay - 1]} 的课程";
        Saved = true;
    }
}