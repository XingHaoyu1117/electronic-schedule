using System;
using System.IO;

namespace ElectronicSchedule;

internal static class Log
{
    private static readonly string Directory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ElectronicSchedule");

    public static string FilePath => Path.Combine(Directory, "error.log");

    public static void Write(Exception? exception)
    {
        if (exception is null)
            return;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // 记录日志失败不影响课表本体运行。
        }
    }
}