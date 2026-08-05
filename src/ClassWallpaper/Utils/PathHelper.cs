using System.IO;

namespace ClassWallpaper.Utils;

/// <summary>
/// 应用存储路径管理（数据根目录自动探测）：
/// 1) 环境变量 CLASSWALLPAPER_ROOT（自定义安装位置）；
/// 2) exe 旁的 ClassWallpaper.root 文件（内容为根目录路径，自定义安装）；
/// 3) D:\ClassWallpaper（目标部署约定，冰点还原只保护 C 盘时数据必须落 D 盘）；
/// 4) 程序所在目录（单盘电脑回退，保证可运行）。
/// </summary>
public static class PathHelper
{
    /// <summary>首选应用根目录（部署约定）。</summary>
    public const string PreferredAppRoot = @"D:\ClassWallpaper";

    /// <summary>根目录配置文件（exe 旁，内容为一行根目录路径）。</summary>
    public const string RootConfigFileName = "ClassWallpaper.root";

    private static readonly string AppRootValue = ResolveAppRoot();

    /// <summary>应用根目录（自动解析：环境变量 / root 文件 / D 盘 / exe 目录）。</summary>
    public static string AppRoot => AppRootValue;

    /// <summary>配置目录。</summary>
    public static string ConfigDir => Path.Combine(AppRootValue, "Config");

    /// <summary>应用设置配置文件（主配置）。</summary>
    public static string ConfigFilePath => Path.Combine(ConfigDir, "settings.json");

    /// <summary>班级用户配置文件。</summary>
    public static string UsersFilePath => Path.Combine(ConfigDir, "users.json");

    /// <summary>壁纸切换计划配置文件。</summary>
    public static string ScheduleFilePath => Path.Combine(ConfigDir, "schedule.json");

    /// <summary>默认壁纸目录（可通过设置页自定义）。</summary>
    public static string WallpapersDir => Path.Combine(AppRootValue, "Wallpapers");

    /// <summary>日志目录。</summary>
    public static string LogsDir => Path.Combine(AppRootValue, "Logs");

    /// <summary>
    /// 解析应用根目录，依次尝试：
    /// 环境变量 CLASSWALLPAPER_ROOT → exe 旁 ClassWallpaper.root 文件 → D 盘 → 程序所在目录。
    /// </summary>
    private static string ResolveAppRoot()
    {
        // 1) 环境变量（最高优先，适合脚本/统一部署）
        var envRoot = Environment.GetEnvironmentVariable("CLASSWALLPAPER_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            if (TryUseRoot(envRoot, out var envPath))
            {
                return envPath;
            }
        }

        // 2) exe 旁的根目录配置文件（内容为一行路径，适合自定义安装）
        var rootConfig = Path.Combine(AppContext.BaseDirectory, RootConfigFileName);
        if (File.Exists(rootConfig))
        {
            try
            {
                var configured = File.ReadAllText(rootConfig).Trim();
                if (configured.Length > 0 && TryUseRoot(configured, out var configPath))
                {
                    return configPath;
                }
            }
            catch
            {
                // 配置文件无效则继续探测
            }
        }

        // 3) 目标部署约定：D:\ClassWallpaper（可写时优先）
        if (TryUseRoot(PreferredAppRoot, out var dRoot))
        {
            return dRoot;
        }

        // 4) 回退：程序所在目录（单盘电脑等）
        return AppContext.BaseDirectory.TrimEnd('\\');
    }

    /// <summary>尝试使用指定根目录（创建并验证可写），成功返回绝对路径。</summary>
    private static bool TryUseRoot(string root, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(root);
            Directory.CreateDirectory(fullPath);
            var probe = Path.Combine(fullPath, "._wtest.tmp");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            fullPath = string.Empty;
            return false;
        }
    }

    /// <summary>确保基础数据目录（配置/日志）存在。</summary>
    public static void EnsureDirectories()
    {
        foreach (var dir in new[] { ConfigDir, LogsDir })
        {
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"无法创建数据目录 {dir}。请确认应用目录可写。",
                    ex);
            }
        }
    }
}
