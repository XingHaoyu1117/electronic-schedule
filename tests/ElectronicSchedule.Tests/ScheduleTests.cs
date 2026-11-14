using System;
using System.Linq;
using ElectronicSchedule;
using Xunit;

namespace ElectronicSchedule.Tests;

public sealed class ScheduleTests
{
    [Fact]
    public void ForDay_ReturnsOnlyLessonsForRequestedWeekday()
    {
        var schedule = new Schedule
        {
            Lessons =
            {
                new Lesson { Day = 1, Start = "08:00", End = "08:45", Name = "语文" },
                new Lesson { Day = 2, Start = "09:00", End = "09:45", Name = "数学" }
            }
        };

        var lessons = schedule.ForDay(new DateTime(2026, 11, 16));

        Assert.Single(lessons);
        Assert.Equal("语文", lessons[0].Name);
    }

    [Fact]
    public void Current_ReturnsLessonOnlyInsideItsTimeRange()
    {
        var schedule = new Schedule
        {
            Lessons =
            {
                new Lesson { Day = 1, Start = "08:00", End = "08:45", Name = "语文" }
            }
        };

        Assert.Equal("语文", schedule.Current(new DateTime(2026, 11, 16, 8, 20, 0))?.Name);
        Assert.Null(schedule.Current(new DateTime(2026, 11, 16, 8, 45, 0)));
    }

    [Fact]
    public void ForDay_OverrideReplacesMatchingLesson()
    {
        var schedule = new Schedule
        {
            Lessons =
            {
                new Lesson { Day = 1, Start = "08:00", End = "08:45", Name = "语文" }
            },
            Overrides =
            {
                new Lesson { Day = 1, Date = "2026-11-16", Start = "08:00", End = "08:45", Name = "班会" }
            }
        };

        var lessons = schedule.ForDay(new DateTime(2026, 11, 16));

        Assert.Single(lessons);
        Assert.Equal("班会", lessons[0].Name);
    }

    [Fact]
    public void Current_ReturnsAddedOverrideWhenNoRegularLessonExists()
    {
        var schedule = new Schedule
        {
            Overrides =
            {
                new Lesson { Day = 1, Date = "2026-11-16", Start = "10:00", End = "10:40", Name = "临时自习" }
            }
        };

        Assert.Equal("临时自习", schedule.Current(new DateTime(2026, 11, 16, 10, 10, 0))?.Name);
    }

    [Fact]
    public void Current_ReturnsNullWhenOverrideCancelsLesson()
    {
        var schedule = new Schedule
        {
            Lessons =
            {
                new Lesson { Day = 1, Start = "08:00", End = "08:45", Name = "语文" }
            },
            Overrides =
            {
                new Lesson { Day = 1, Date = "2026-11-16", Start = "08:00", End = "08:45", Name = "" }
            }
        };

        Assert.Null(schedule.Current(new DateTime(2026, 11, 16, 8, 10, 0)));
    }
}
