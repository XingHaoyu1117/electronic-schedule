using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ElectronicSchedule;

/// <summary>
/// 开机自动启动。写当前用户的 Run 注册表项，不需要管理员权限，
/// 也不在系统里留下快捷方式文件。
/// </summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "电子课表";

    public static bool Enabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch (Exception ex)
            {
                Log.Write(ex);
                return false;
            }
        }
    }

    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);

            if (key is null)
                return false;

            if (enabled)
                key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);

            return true;
        }
        catch (Exception ex)
        {
            Log.Write(ex);
            return false;
        }
    }
}