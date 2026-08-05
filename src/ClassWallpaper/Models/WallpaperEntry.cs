namespace ClassWallpaper.Models;

/// <summary>壁纸绑定条目：人员姓名与对应壁纸图片（Wallpapers 目录下"姓名.jpg"）。</summary>
public sealed class WallpaperEntry
{
    /// <summary>人员姓名（对应 users.json）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>壁纸图片完整路径；人员无对应图片时为 null。</summary>
    public string? ImagePath { get; set; }

    /// <summary>是否有对应图片。</summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);
}
