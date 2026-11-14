using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ElectronicSchedule;

/// <summary>当天课表列表，左侧窄栏和顶部横条展开后都用它。</summary>
public sealed class LessonList : FlowLayoutPanel
{
    private readonly Font font;
    private readonly Font fontActive;
    private readonly Color rowBack;
    private readonly Color rowBackActive;
    private readonly Color textColor;
    private readonly Color textActive;
    private readonly List<Label> rows = new();

    private int rowHeight = 58;
    private int gap = 8;
    private int activeIndex = -1;

    public LessonList(Font font, Font fontActive, Color rowBack, Color rowBackActive,
        Color textColor, Color textActive)
    {
        this.font = font;
        this.fontActive = fontActive;
        this.rowBack = rowBack;
        this.rowBackActive = rowBackActive;
        this.textColor = textColor;
        this.textActive = textActive;

        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = true;
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
    }

    public void Configure(int rowHeightPx, int spacing)
    {
        rowHeight = rowHeightPx;
        gap = spacing;
    }

    /// <summary>所有行占用的高度。rowCount 传入待显示的行数，未 build 时也能算出高度。</summary>
    public int RowsHeight(int rowCount) => rowCount * (rowHeight + gap);

    public void Build(IReadOnlyList<Lesson> lessons, int width)
    {
        SuspendLayout();

        foreach (var row in rows)
            row.Dispose();

        rows.Clear();
        Controls.Clear();
        activeIndex = -1;

        if (lessons.Count == 0)
        {
            var empty = NewRow("今天没有安排课程");
            empty.ForeColor = Color.Gray;
            rows.Add(empty);
            Controls.Add(empty);
        }
        else
        {
            for (int i = 0; i < lessons.Count; i++)
            {
                var lesson = lessons[i];
                var row = NewRow($"{i + 1}. {lesson.Name}\n{lesson.Start} - {lesson.End}");
                row.Tag = i;
                rows.Add(row);
                Controls.Add(row);
            }
        }

        SetRowWidth(width);
        ResumeLayout();
    }

    public void SetRowWidth(int width)
    {
        foreach (var row in rows)
            row.Width = Math.Max(80, width);
    }

    public void SetActive(int index)
    {
        if (index == activeIndex)
            return;

        if (activeIndex >= 0 && activeIndex < rows.Count && rows[activeIndex].Tag is int)
        {
            rows[activeIndex].Font = font;
            rows[activeIndex].ForeColor = textColor;
            rows[activeIndex].BackColor = rowBack;
        }

        if (index >= 0 && index < rows.Count && rows[index].Tag is int)
        {
            rows[index].Font = fontActive;
            rows[index].ForeColor = textActive;
            rows[index].BackColor = rowBackActive;
        }

        activeIndex = index;
    }

    private Label NewRow(string text) => new()
    {
        AutoSize = false,
        Height = rowHeight,
        Margin = new Padding(0, 0, 0, gap),
        Padding = new Padding(14, 8, 8, 8),
        Font = font,
        ForeColor = textColor,
        BackColor = rowBack,
        TextAlign = ContentAlignment.MiddleLeft,
        UseMnemonic = false,
        Text = text
    };
}