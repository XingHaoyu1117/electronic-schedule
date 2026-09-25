using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ElectronicSchedule;

public sealed class MainForm : Form
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly Timer timer = new() { Interval = 1000 };
    private readonly Label dateLabel = new();
    private readonly Label currentLabel = new();
    private readonly FlowLayoutPanel list = new();
    private readonly Button tabButton = new();
    private bool expanded = true;
    private bool hiddenByFullscreen;
    private DateTime fullscreenEndedAt;

    private readonly (TimeSpan start, TimeSpan end, string name)[] lessons =
    {
        (new(8,0,0),  new(8,45,0),  "语文"),
        (new(8,55,0), new(9,40,0),  "数学"),
        (new(9,50,0), new(10,35,0), "英语"),
        (new(10,45,0),new(11,30,0), "物理"),
        (new(14,0,0), new(14,45,0), "化学"),
        (new(14,55,0),new(15,40,0), "体育"),
        (new(15,50,0),new(16,35,0), "历史")
    };

    public MainForm()
    {
        Text = "电子课表";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(24, 28, 34);
        Width = 360;
        Height = 760;

        dateLabel.Font = new Font("Microsoft YaHei UI", 18, FontStyle.Bold);
        dateLabel.ForeColor = Color.White;
        dateLabel.AutoSize = true;
        dateLabel.Location = new Point(28, 28);

        currentLabel.Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold);
        currentLabel.ForeColor = Color.FromArgb(110, 220, 160);
        currentLabel.AutoSize = true;
        currentLabel.Location = new Point(28, 72);

        list.FlowDirection = FlowDirection.TopDown;
        list.WrapContents = false;
        list.AutoScroll = true;
        list.BackColor = Color.Transparent;
        list.Location = new Point(20, 125);
        list.Size = new Size(320, 560);

        tabButton.Text = "课\n表";
        tabButton.Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold);
        tabButton.ForeColor = Color.White;
        tabButton.BackColor = Color.FromArgb(35, 110, 80);
        tabButton.FlatStyle = FlatStyle.Flat;
        tabButton.FlatAppearance.BorderSize = 0;
        tabButton.Size = new Size(54, 100);
        tabButton.Location = new Point(-54, 250);
        tabButton.Click += (_, _) => TogglePanel();
        Controls.Add(tabButton);

        Controls.Add(dateLabel);
        Controls.Add(currentLabel);
        Controls.Add(list);

        Resize += (_, _) => PositionRight();
        Shown += (_, _) => PositionRight();

        timer.Tick += (_, _) => UpdateState();
        timer.Start();
        UpdateState();
    }

    private void TogglePanel()
    {
        expanded = !expanded;
        Width = expanded ? 360 : 54;
        dateLabel.Visible = currentLabel.Visible = list.Visible = expanded;
        tabButton.Location = expanded ? new Point(-54, 250) : new Point(0, 250);
        PositionRight();
    }

    private void PositionRight()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.PrimaryScreen!.Bounds;
        Left = area.Right - Width;
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    private void UpdateState()
    {
        var now = DateTime.Now;
        dateLabel.Text = now.ToString("M月d日  dddd");
        var current = GetCurrentLesson(now.TimeOfDay);
        currentLabel.Text = current is null ? "当前无课程" : $"正在上：{current.Value.name}";

        list.Controls.Clear();
        for (int i = 0; i < lessons.Length; i++)
        {
            var x = lessons[i];
            var active = current.HasValue && current.Value.start == x.start;
            var item = new Label
            {
                Width = 300,
                Height = 58,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(16, 8, 8, 8),
                Font = new Font("Microsoft YaHei UI", 13, active ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = active ? Color.White : Color.Gainsboro,
                BackColor = active ? Color.FromArgb(35, 125, 90) : Color.FromArgb(38, 44, 52),
                Text = $"{i + 1}. {x.name}\n{x.start:hh\\:mm} - {x.end:hh\\:mm}"
            };
            list.Controls.Add(item);
        }

        HandleFullscreen(now);
    }

    private (TimeSpan start, TimeSpan end, string name)? GetCurrentLesson(TimeSpan t)
    {
        foreach (var x in lessons)
            if (t >= x.start && t < x.end) return x;
        return null;
    }

    private void HandleFullscreen(DateTime now)
    {
        var fg = GetForegroundWindow();
        if (fg == Handle) return;

        if (GetWindowRect(fg, out var r))
        {
            var area = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
            bool covers = r.Left <= area.Left && r.Top <= area.Top &&
                          r.Right >= area.Right && r.Bottom >= area.Bottom;

            if (covers)
            {
                if (Visible)
                {
                    hiddenByFullscreen = true;
                    Hide();
                }
                return;
            }
        }

        if (hiddenByFullscreen && (now - fullscreenEndedAt).TotalMilliseconds > 1200)
        {
            hiddenByFullscreen = false;
            Show();
            TopMost = true;
            PositionRight();
        }
        else if (!Visible)
        {
            fullscreenEndedAt = now;
        }
    }
}
