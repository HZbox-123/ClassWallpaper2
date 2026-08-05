namespace ClassWallpaper.Models;

/// <summary>壁纸扫描结果：绑定关系、缺失图片人员、多余图片、默认壁纸。</summary>
public sealed class WallpaperScanResult
{
    /// <summary>已绑定壁纸的人员。</summary>
    public List<WallpaperEntry> Bindings { get; } = new();

    /// <summary>没有对应壁纸图片的人员姓名。</summary>
    public List<string> MissingNames { get; } = new();

    /// <summary>目录中存在但无对应人员的图片文件（完整路径，不含默认壁纸）。</summary>
    public List<string> OrphanFiles { get; } = new();

    /// <summary>默认壁纸路径（Wallpapers\默认.jpg 等，缺失时为 null）。</summary>
    public string? DefaultWallpaper { get; set; }
}
