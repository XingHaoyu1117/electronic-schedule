using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ElectronicSchedule;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Write(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write(e.ExceptionObject as Exception);

        // 带 --edit 参数时直接打开课表编辑器；带 xlsx 路径则先导入。
        var args = Environment.GetCommandLineArgs();
        string? importPath = args
            .Skip(1)
            .FirstOrDefault(a => a.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));

        if (args.Any(a => a.Equals("--edit", StringComparison.OrdinalIgnoreCase)) || importPath is not null)
        {
            var editor = new EditorForm();

            if (importPath is not null)
                editor.StartImport(importPath);

            Application.Run(editor);
            return;
        }

        // 教室机上常被重复双击，只允许一个实例常驻。
        using var mutex = new Mutex(true, @"Local\ElectronicSchedule.SingleInstance", out bool createdNew);
        if (!createdNew)
            return;

        Application.Run(new TopBarForm(Schedule.LoadOrDefault()));
    }
}