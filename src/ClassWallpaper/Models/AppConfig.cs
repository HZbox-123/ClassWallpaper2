using ClassWallpaper.Utils;

namespace ClassWallpaper.Models;

/// <summary>
/// 应用设置模型，对应 D:\ClassWallpaper\Config\settings.json。
/// 目标部署电脑安装 Deep Freeze 且只保护 C 盘，因此配置必须保存在 D 盘。
/// </summary>
public sealed class AppConfig
{
    /// <summary>配置结构版本号，后续结构变更时用于迁移。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>日志文件保留天数（按天滚动）。</summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>上次已执行的排班日期（SchedulerService 维护，用于错过补执行去重）。</summary>
    public DateTime? LastAppliedDate { get; set; }

    /// <summary>壁纸目录（可自定义；默认 D:\ClassWallpaper\Wallpapers）。</summary>
    public string WallpapersDir { get; set; } = PathHelper.WallpapersDir;

    /// <summary>壁纸填充方式：Fit（适应）/ Stretch（拉伸）/ Center（居中）/ Tile（平铺）。</summary>
    public string WallpaperStyle { get; set; } = "Fit";

    /// <summary>启动时是否显示主窗口（false = 直接后台托盘驻留）。</summary>
    public bool ShowMainWindowOnStartup { get; set; } = true;

    /// <summary>定时换壁纸检查间隔（小时，默认 0.25 = 15 分钟）。</summary>
    public double RotationIntervalHours { get; set; } = 0.25;
}

