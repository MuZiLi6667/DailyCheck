using Microsoft.Win32;

namespace DailyCheck.Services;

/// <summary>开机自启：写当前用户的 Run 注册表项，不需要管理员权限</summary>
public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DailyCheck";

    /// <summary>当前 exe 的完整路径</summary>
    public static string ExePath => Environment.ProcessPath ?? "";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    /// <summary>设置开机自启，返回是否成功</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return false;
            if (enabled) key.SetValue(AppName, $"\"{ExePath}\"");
            else key.DeleteValue(AppName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
