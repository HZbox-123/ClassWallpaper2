using System;
using System.IO;
using Microsoft.Win32;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 开机自启实现：HKCU\Software\Microsoft\Windows\CurrentVersion\Run 下写入
/// ClassWallpaper = "exe路径"。当前用户键无需管理员权限；
/// 目标部署电脑在解冻状态下设置一次即可被 Deep Freeze 快照保存。
/// </summary>
public sealed class AutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _runKeyName;
    private readonly string _appPath;

    public AutoStartService()
        : this("ClassWallpaper", Path.Combine(AppContext.BaseDirectory, "ClassWallpaper.exe"))
    {
    }

    /// <summary>便于测试：可指定注册表键名与程序路径。</summary>
    public AutoStartService(string runKeyName, string appPath)
    {
        _runKeyName = runKeyName;
        _appPath = appPath;
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(_runKeyName) as string;
            // 兼容新旧值：包含程序路径即视为已开启（新值带 -silent 静默参数）
            return !string.IsNullOrEmpty(value)
                && value.Contains(_appPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取开机自启状态失败");
            return false;
        }
    }

    public void Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            // 带 -silent 参数：开机自启时静默后台运行（不弹主界面）
            key.SetValue(_runKeyName, $"\"{_appPath}\" -silent");
            Log.Information("已开启开机自启：{Path}", _appPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"开启开机自启失败:{ex.Message}", ex);
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(_runKeyName, throwOnMissingValue: false);
            Log.Information("已关闭开机自启");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"关闭开机自启失败:{ex.Message}", ex);
        }
    }
}

