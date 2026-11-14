using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ElectronicSchedule;

/// <summary>屏幕顶端居中的半透明课程条。交互形态参考 ClassIsland 的灵动岛式主界面。</summary>
public sealed class TopBarForm : OverlayForm
{
    private const int BarHeight = 40;
    private const int Radius = 10;
    private const int PadX = 12;
    private const int Gap = 7;
    private const int DateWidth = 132;
    private const int CurrentWidth = 196;
    private const int NextWidth = 100;
    private const int ClockWidth = 88;
    private const int OverrideChip = 18;
    private const int RowHeight = 38;
    private const int RowGap = 4;
    private const int TopMargin = 0;

    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");

    private static readonly Color BarBack = Color.FromArgb(26, 30, 36);
    private static readonly Color BarBackHover = Color.FromArgb(40, 46, 55);
    private static readonly Color BarBackReminder = Color.FromArgb(92, 62, 18);

    private readonly Label dateLabel = new();
    private readonly Label currentLabel = new();
    private readonly Label nextLabel = new();
    private readonly Label clockLabel = new();
    private readonly Label overrideChip = new();
    private readonly Panel[] dividers = { new(), new(), new() };
    private readonly Panel progressTrack = new();
    private readonly Panel progressFill = new();
    private readonly LessonList list;
    private readonly NotifyIcon tray;


    private readonly Font dateFont = new("Microsoft YaHei UI", 9.5f);
    private readonly Font currentFont = new("Microsoft YaHei UI", 12f, FontStyle.Bold);
    private readonly Font secondaryFont = new("Microsoft YaHei UI", 9f);
    private readonly Font clockFont = new("Consolas", 11f, FontStyle.Bold);
    private readonly Font rowFont = new("Microsoft YaHei UI", 9.5f);
    private readonly Font rowFontActive = new("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
    private readonly Font chipFont = new("Microsoft YaHei UI", 8.5f, FontStyle.Bold);

    private bool expanded;
    private Lesson? current;
    private IReadOnlyList<Lesson> today = Array.Empty<Lesson>();
    private DateTime renderedDay = DateTime.MinValue;
    private int renderedCount = -1;

    // 提醒相关：记录今天已经响过的铃，避免同一节课反复触发。
    private readonly HashSet<string> fired = new();
    private string firedDay = string.Empty;
    private DateTime bannerUntil = DateTime.MinValue;
    private string bannerText = string.Empty;
    private bool hovered;

    public TopBarForm(Schedule schedule) : base(schedule, Color.FromArgb(26, 30, 36))
    {
        dateLabel.Font = dateFont;
        dateLabel.ForeColor = Color.FromArgb(214, 220, 228);
        currentLabel.Font = currentFont;
        currentLabel.ForeColor = Color.White;
        nextLabel.Font = secondaryFont;
        nextLabel.ForeColor = Color.FromArgb(150, 160, 172);
        clockLabel.Font = clockFont;
        clockLabel.ForeColor = Color.FromArgb(214, 220, 228);

        foreach (var label in new[] { dateLabel, currentLabel, nextLabel, clockLabel })
        {
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.AutoEllipsis = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        clockLabel.TextAlign = ContentAlignment.MiddleRight;

        // 顶部横条带"今天有调课"的小角标，平时不占位置。
        overrideChip.Font = chipFont;
        overrideChip.ForeColor = Color.FromArgb(255, 226, 150);
        overrideChip.TextAlign = ContentAlignment.MiddleCenter;
        overrideChip.AutoSize = false;
        overrideChip.UseMnemonic = false;
        overrideChip.Text = "调";
        overrideChip.BackColor = Color.FromArgb(70, 58, 30);
        overrideChip.Visible = false;

        foreach (var divider in dividers)
            divider.BackColor = Color.FromArgb(58, 66, 78);

        progressTrack.BackColor = Color.FromArgb(102, 110, 124);
        progressFill.BackColor = Color.FromArgb(130, 240, 180);

        list = new LessonList(rowFont, rowFontActive,
            Color.FromArgb(38, 44, 52), Color.FromArgb(35, 125, 90),
            Color.Gainsboro, Color.White)
        {
            Visible = false
        };

        BackColor = BarBack;
        Controls.Add(dateLabel);
        Controls.Add(currentLabel);
        Controls.Add(nextLabel);
        Controls.Add(clockLabel);
        Controls.Add(overrideChip);
        Controls.AddRange(dividers);
        Controls.Add(progressTrack);
        Controls.Add(progressFill);
        progressFill.BringToFront();
        Controls.Add(list);

        EventHandler toggle = (_, _) => ToggleExpand();
        foreach (Control control in new Control[] { dateLabel, currentLabel, nextLabel, clockLabel, progressTrack })
            control.Click += toggle;
        Click += toggle;

        // 悬停时整条变亮一点，鼠标移开恢复，给"可以点"一个反馈。
        foreach (Control control in new Control[] { dateLabel, currentLabel, nextLabel, clockLabel, overrideChip, progressTrack })
        {
            control.MouseEnter += (_, _) => SetHover(true);
            control.MouseLeave += (_, _) => SetHover(false);
            control.Cursor = Cursors.Hand;
        }

        MouseEnter += (_, _) => SetHover(true);
        MouseLeave += (_, _) => SetHover(false);

        Cursor = Cursors.Hand;

        tray = BuildTray();

        Relayout();
    }

    protected override void OnTick(DateTime now)
    {
        dateLabel.Text = now.ToString("M月d日", Zh) + " " + now.ToString("ddd", Zh);
        clockLabel.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        today = Schedule.ForDay(now);
        current = Schedule.Current(now);

        bool adjusted = Schedule.HasOverrides(now);
        overrideChip.Visible = adjusted;

        if (current is null)
        {
            currentLabel.Text = today.Count == 0 ? "今天没有课" : "当前无课程";
            progressFill.Width = 0;
        }
        else
        {
            currentLabel.Text = $"{current.Name}  {current.Start}-{current.End}";
            progressFill.Width = (int)(S(CurrentWidth) * Progress(current, now));
        }

        var next = NextLesson(now);
        nextLabel.Text = next is not null
            ? $"下节 {next.Name} {next.Start}"
            : today.Count == 0 ? string.Empty : "今日结束";

        CheckReminders(now);

        // 响铃期间横条换成醒目文字，几秒后自动恢复。
        if (now < bannerUntil && bannerText.Length > 0)
        {
            currentLabel.Text = bannerText;
            progressFill.Width = 0;
        }

        ApplyBarColor(now);

        if (expanded)
            EnsureList(now);
    }

    private void SetHover(bool entered)
    {
        hovered = entered;

        if (DateTime.Now >= bannerUntil)
            BackColor = entered ? BarBackHover : BarBack;
    }

    /// <summary>悬停变亮；响铃时用暖色，让人一眼注意到。</summary>
    private void ApplyBarColor(DateTime now)
    {
        TimeSpan since = now - bannerUntil;

        if (since.TotalSeconds > 0)
        {
            BackColor = hovered ? BarBackHover : BarBack;
        }
        else
        {
            BackColor = BarBackReminder;
        }
    }

    /// <summary>
    /// 检查预备铃、上课铃、下课铃。横条被全屏隐藏时依然会响，
    /// 因为老师放课件时正是最需要听铃的时候。
    /// </summary>
    private void CheckReminders(DateTime now)
    {
        var options = Schedule.Reminders;

        if (!options.Enabled)
            return;

        string day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (firedDay != day)
        {
            fired.Clear();
            firedDay = day;
        }

        foreach (var lesson in today)
        {
            if (options.Prepare && options.PrepareMinutes > 0)
            {
                var prepareAt = lesson.StartTime - TimeSpan.FromMinutes(options.PrepareMinutes);
                if (Reached(now.TimeOfDay, prepareAt) &&
                    fired.Add($"{day}|{lesson.Start}|prep"))
                {
                    Chime.PlayPrepare();
                    ShowBanner(now, $"{lesson.Name} · {options.PrepareMinutes} 分钟后上课");
                }
            }

            if (options.Start && Reached(now.TimeOfDay, lesson.StartTime) &&
                fired.Add($"{day}|{lesson.Start}|start"))
            {
                Chime.PlayStart();
                ShowBanner(now, $"上课 · {lesson.Name}");
            }

            if (options.End && Reached(now.TimeOfDay, lesson.EndTime) &&
                fired.Add($"{day}|{lesson.End}|end"))
            {
                Chime.PlayEnd();
                ShowBanner(now, $"下课 · {lesson.Name}");
            }
        }
    }

    /// <summary>这一刻是否刚跨过目标时间。1 秒一次的刷新频率下留一点容差。</summary>
    private static bool Reached(TimeSpan now, TimeSpan target)
    {
        if (target < TimeSpan.Zero)
            return false;

        return now >= target && now - target < TimeSpan.FromSeconds(1.5);
    }

    private void ShowBanner(DateTime now, string text)
    {
        bannerText = text;
        bannerUntil = now.AddSeconds(Math.Max(3, Schedule.Reminders.BannerSeconds));
    }

    protected override void Relayout()
    {
        int padX = S(PadX);
        int gap = S(Gap);
        int barHeight = S(BarHeight);

        int width = S(DateWidth) + S(CurrentWidth) + S(NextWidth) + S(ClockWidth)
                    + 3 * (S(1) + 2 * gap) + padX * 2;

        list.Visible = expanded;

        int listHeight = 0;
        if (expanded)
        {
            list.Configure(S(RowHeight), S(RowGap));
            int rowCount = Math.Max(1, today.Count);
            int wanted = list.RowsHeight(rowCount) + S(8);
            int available = PrimaryArea.Height - barHeight - S(24);
            listHeight = Math.Max(S(RowHeight), Math.Min(wanted, available));
        }

        Width = width;
        SetHeight(barHeight + listHeight, Schedule.Appearance.AnimateExpand);
        list.Visible = expanded;

        int x = padX;

        dateLabel.Bounds = new Rectangle(x, 0, S(DateWidth) - S(OverrideChip) - S(4), barHeight);
        overrideChip.Bounds = new Rectangle(
            x + S(DateWidth) - S(OverrideChip), S(11), S(OverrideChip), S(OverrideChip));
        x += S(DateWidth) + gap;
        dividers[0].Bounds = new Rectangle(x, S(10), S(1), barHeight - S(20));
        x += S(1) + gap;

        currentLabel.Bounds = new Rectangle(x, 0, S(CurrentWidth), barHeight - S(8));
        progressTrack.Bounds = new Rectangle(x, barHeight - S(6), S(CurrentWidth), S(3));
        progressFill.Bounds = new Rectangle(x, barHeight - S(6), progressFill.Width, S(3));
        x += S(CurrentWidth) + gap;
        dividers[1].Bounds = new Rectangle(x, S(10), S(1), barHeight - S(20));
        x += S(1) + gap;

        nextLabel.Bounds = new Rectangle(x, 0, S(NextWidth), barHeight);
        x += S(NextWidth) + gap;
        dividers[2].Bounds = new Rectangle(x, S(10), S(1), barHeight - S(20));
        x += S(1) + gap;

        clockLabel.Bounds = new Rectangle(x, 0, S(ClockWidth), barHeight);

        if (expanded)
        {
            list.Bounds = new Rectangle(padX, barHeight + S(2), width - padX * 2, Math.Max(S(20), listHeight - S(4)));
            list.SetRowWidth(width - padX * 2 - S(24));
        }
        else
        {
            list.Bounds = new Rectangle(padX, barHeight, width - padX * 2, 0);
        }

        ApplyRoundedRegion(Radius);
        CenterAtTop(TopMargin);
    }

    protected override void OnContentInvalidated()
    {
        renderedDay = DateTime.MinValue;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tray.Visible = false;
            tray.Dispose();

            dateFont.Dispose();
            currentFont.Dispose();
            secondaryFont.Dispose();
            clockFont.Dispose();
            rowFont.Dispose();
            rowFontActive.Dispose();
            chipFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private NotifyIcon BuildTray()
    {
        var menu = new ContextMenuStrip();

        var reminderItem = new ToolStripMenuItem("上下课提醒")
        {
            CheckOnClick = true,
            Checked = Schedule.Reminders.Enabled
        };
        reminderItem.CheckedChanged += (_, _) =>
        {
            Schedule.Reminders.Enabled = reminderItem.Checked;
            SaveQuietly();
        };

        var autoStartItem = new ToolStripMenuItem("开机自动启动")
        {
            CheckOnClick = true,
            Checked = AutoStart.Enabled
        };
        autoStartItem.CheckedChanged += (_, _) =>
        {
            if (!AutoStart.Set(autoStartItem.Checked))
            {
                autoStartItem.Checked = !autoStartItem.Checked;
                MessageBox.Show("无法修改开机启动项，可能是权限不足。", "电子课表",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        menu.Items.Add("编辑课表…", null, (_, _) => OpenEditor());
        menu.Items.Add("展开/收起课表", null, (_, _) => ToggleExpand());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(reminderItem);
        menu.Items.Add("试听铃声", null, (_, _) => Chime.Preview());
        menu.Items.Add(autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("重新载入课表", null, (_, _) => ReloadSchedule());
        menu.Items.Add("退出", null, (_, _) => Close());

        var icon = new NotifyIcon
        {
            Text = "电子课表",
            Icon = CreateTrayIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        icon.DoubleClick += (_, _) => OpenEditor();
        return icon;
    }

    /// <summary>托盘开关改动后写回配置，失败不打断使用。</summary>
    private void SaveQuietly()
    {
        try
        {
            Schedule.Save();
        }
        catch (Exception ex)
        {
            Log.Write(ex);
        }
    }

    private void OpenEditor()
    {
        using var editor = new EditorForm();
        editor.ShowDialog();

        if (editor.Saved)
            ReloadSchedule();
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(35, 125, 90));
            graphics.FillEllipse(brush, 1, 1, 30, 30);

            using var font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            var size = graphics.MeasureString("课", font);
            graphics.DrawString("课", font, Brushes.White,
                (32 - size.Width) / 2, (32 - size.Height) / 2);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void ToggleExpand()
    {
        expanded = !expanded;
        renderedDay = DateTime.MinValue;

        // 先建好行，Relayout 里才能按实际行数算出展开高度；可见性由 Relayout 统一处理。
        if (expanded)
            EnsureList(DateTime.Now);

        Relayout();
    }

    private void EnsureList(DateTime now)
    {
        if (renderedDay.Date != now.Date || renderedCount != today.Count)
        {
            list.Build(today, Width - S(PadX) * 2 - S(24));
            renderedDay = now.Date;
            renderedCount = today.Count;
        }

        // ForDay 每次返回新对象，按课程名和时段比对，而不是引用。
        int index = -1;
        if (current is not null)
        {
            for (int i = 0; i < today.Count; i++)
            {
                if (today[i].Name == current.Name && today[i].StartTime == current.StartTime)
                {
                    index = i;
                    break;
                }
            }
        }

        list.SetActive(index);
    }

    private Lesson? NextLesson(DateTime now)
    {
        Lesson? best = null;

        foreach (var lesson in today)
        {
            if (lesson.StartTime <= now.TimeOfDay)
                continue;

            if (best is null || lesson.StartTime < best.StartTime)
                best = lesson;
        }

        return best;
    }

    private static double Progress(Lesson lesson, DateTime now)
    {
        double total = (lesson.EndTime - lesson.StartTime).TotalSeconds;
        if (total <= 0)
            return 0;

        return Math.Clamp((now.TimeOfDay - lesson.StartTime).TotalSeconds / total, 0, 1);
    }
}
