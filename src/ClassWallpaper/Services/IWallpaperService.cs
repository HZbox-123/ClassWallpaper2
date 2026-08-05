using ClassWallpaper.Models;

namespace ClassWallpaper.Services;

/// <summary>壁纸服务：扫描 Wallpapers 目录生成绑定关系，并通过 Windows API 设置桌面壁纸。</summary>
public interface IWallpaperService
{
    /// <summary>
    /// 扫描壁纸目录并与 users.json 人员绑定，返回绑定、缺失、多余图片清单。
    /// </summary>
    WallpaperScanResult Scan();

    /// <summary>
    /// 将指定图片设置为桌面壁纸（调用 Windows API SystemParametersInfo）。
    /// 路径为空或文件不存在时抛出异常。
    /// </summary>
    void SetWallpaper(string imagePath);
}
