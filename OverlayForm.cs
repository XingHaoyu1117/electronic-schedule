using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ElectronicSchedule;

/// <summary>悬浮窗口的公共行为：置顶、不抢焦点、圆角、半透明、全屏自动隐藏、DPI 缩放。</summary>
public abstract class OverlayForm : Form
{
    private const double RestoreDelaySeconds = 1.2;
    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    /// <summary>桌面、任务栏、开始菜单这类系统界面，不能当成"全屏窗口"。</summary>
    private static readonly string[] ShellClasses =
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "MultitaskingViewFrame", "ForegroundStaging"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer transition = new() { Interval = 15 };
    private bool hiddenByFullscreen;
    private DateTime fullscreenLostAt = DateTime.MinValue;
    private float scale = 1f;

    private DateTime transitionStart = DateTime.MinValue;
    private int transitionFromHeight;
    private int transitionToHeight;
    private readonly DateTime startedAt = DateTime.Now;

    /// <summary>高度已变化的回调，子类可用来在展开/收起后同步列表可见性。</summary>
    protected event Action? HeightChanged;

    protected OverlayForm(Schedule schedule, Color background)
    {
        Schedule = schedule;
        UpdateScale();

        Text = schedule.Title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false; // 后台静默运行，不占任务栏
        AutoScaleMode = AutoScaleMode.None;
        BackColor = background;
        AllowTransparency = true;
        Opacity = Math.Clamp(schedule.Appearance.Opacity, 0.3, 1.0);

        clock.Tick += (_, _) => Tick();
        transition.Tick += (_, _) => StepTransition();
    }

    protected Schedule Schedule { get; private set; }

    protected static Rectangle PrimaryArea =>
        Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

    /// <summary>按当前 DPI 缩放一个 96 DPI 下的设计值。</summary>
    protected int S(int value) => (int)Math.Round(value * scale);

    /// <summary>按屏幕宽度和系统 DPI 缩放字体，1920 宽屏上提高后排可读性。</summary>
    protected float SF(float value) => value * scale;

    private void UpdateScale()
    {
        float dpiScale = Math.Max(1f, DeviceDpi / 96f);
        float screenScale = Math.Clamp(PrimaryArea.Width / 1440f, 1f, 1.4f);
        scale = dpiScale * screenScale;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Tick();
        Relayout();
        clock.Start();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UpdateScale();
        Relayout();
        OnContentInvalidated();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            clock.Stop();
            clock.Dispose();
            transition.Stop();
            transition.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>重新读取 schedule.json 并立即生效。</summary>
    protected void ReloadSchedule()
    {
        Schedule = ElectronicSchedule.Schedule.LoadOrDefault();
        OnContentInvalidated();
        Relayout();
        Tick();
    }

    /// <summary>每秒刷新一次窗口内容。</summary>
    protected abstract void OnTick(DateTime now);

    /// <summary>重新计算窗口位置与所有子控件边界。</summary>
    protected abstract void Relayout();

    /// <summary>DPI 或课表变化后需要重建的内容。</summary>
    protected virtual void OnContentInvalidated()
    {
    }

    private void Tick()
    {
        UpdateScale();
        var now = DateTime.Now;
        OnTick(now);
        TickFullscreen(now);
    }

    protected void ApplyRoundedRegion(int radius)
    {
        int r = S(radius);
        if (r <= 0)
            return;

        using var path = new GraphicsPath();
        int d = r * 2;
        var rect = new Rectangle(0, 0, Width, Height);

        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected void CenterAtTop(int topMargin)
    {
        var area = PrimaryArea;
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = area.Top + S(topMargin);
    }

    /// <summary>
    /// 调整窗口高度：animate 为 true 时带下滑/收起动画，否则立即生效（例如启动时）。
    /// </summary>
    protected void SetHeight(int targetHeight, bool animate)
    {
        transition.Stop();

        if (!animate || !Visible)
        {
            Height = targetHeight;
            HeightChanged?.Invoke();
            return;
        }

        // 记录起止高度，由定时器按 ease-out 曲线逐帧更新窗口高度。
        // 子控件按最终尺寸布局，窗口区域会在 OnResize 中同步更新。
        if (Height == targetHeight)
            return;

        transitionFromHeight = Height;
        transitionToHeight = targetHeight;
        transitionStart = DateTime.Now;
        HeightChanged?.Invoke();
        transition.Start();
    }

    private void StepTransition()
    {
        const double duration = 0.16;

        double elapsed = (DateTime.Now - transitionStart).TotalSeconds;
        double progress = Math.Clamp(elapsed / duration, 0, 1);

        // ease-out：开头快、结尾慢，看起来比线性自然。
        double eased = 1 - Math.Pow(1 - progress, 3);
        Height = transitionFromHeight + (int)Math.Round((transitionToHeight - transitionFromHeight) * eased);

        if (progress >= 1)
        {
            transition.Stop();
            Height = transitionToHeight;
        }
    }

    private void TickFullscreen(DateTime now)
    {
        var foreground = GetForegroundWindow();
        bool covers = foreground != IntPtr.Zero
                      && foreground != Handle
                      && !IsShellSurface(foreground)
                      && IsFullscreen(foreground);

        if (covers)
        {
            // 启动阶段不做隐藏，避免被误判后整个过程都不显示。
            if (Visible && now - startedAt > TimeSpan.FromSeconds(3))
            {
                hiddenByFullscreen = true;
                Hide();
            }

            fullscreenLostAt = DateTime.MinValue;
            return;
        }

        if (!hiddenByFullscreen && Visible)
            return;

        if (fullscreenLostAt == DateTime.MinValue)
        {
            fullscreenLostAt = now;
            return;
        }

        if ((now - fullscreenLostAt).TotalSeconds < RestoreDelaySeconds)
            return;

        hiddenByFullscreen = false;
        fullscreenLostAt = DateTime.MinValue;
        Show();
        TopMost = true;
        Relayout();
    }

    private static bool IsShellSurface(IntPtr window)
    {
        var className = new StringBuilder(64);
        if (GetClassName(window, className, className.Capacity) == 0)
            return false;

        string name = className.ToString();

        foreach (var shell in ShellClasses)
        {
            if (string.Equals(name, shell, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 判定"占满屏幕"的窗口，命中时横条隐藏：
    /// 真全屏（含 F11、视频播放器）、PPT 放映、以及铺满整个屏幕但保留任务栏的"半全屏"窗口。
    /// 普通带标题栏的最大化窗口不算，否则随便开个最大化程序横条就没了。
    /// </summary>
    private static bool IsFullscreen(IntPtr window)
    {
        if (!GetWindowRect(window, out var rect))
            return false;

        var screen = Screen.FromHandle(window);
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;

        bool coversAll = rect.Left <= bounds.Left && rect.Top <= bounds.Top &&
                         rect.Right >= bounds.Right && rect.Bottom >= bounds.Bottom;

        bool coversWork = rect.Left <= work.Left && rect.Top <= work.Top &&
                          rect.Right >= work.Right && rect.Bottom >= work.Bottom;

        if (!coversAll && !coversWork)
            return false;

        // 无边框窗口本身就可能是浏览器全屏，这一条优先。
        int style = GetWindowLong(window, GWL_STYLE);
        if ((style & WS_CAPTION) == 0)
            return true;

        // 带标题栏：只有它连任务栏区域一起盖住时才算全屏，
        // 普通最大化会停在任务栏上沿，所以不会误判。
        return rect.Bottom > work.Bottom + 4;
    }
}