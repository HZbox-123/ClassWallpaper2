using System.Reflection;

namespace ClassWallpaper.Models;

/// <summary>应用元信息（静态常量）。</summary>
public static class AppInfo
{
    /// <summary>应用显示名称。</summary>
    public const string AppName = "ClassWallpaper 班级自动壁纸管理系统";

    /// <summary>程序集版本号。</summary>
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
