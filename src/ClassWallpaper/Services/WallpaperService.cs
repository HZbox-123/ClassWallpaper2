using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ClassWallpaper.Models;
using ClassWallpaper.Utils;
using Microsoft.Win32;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 壁纸服务实现：
/// - Scan：扫描壁纸目录（默认 D:\ClassWallpaper\Wallpapers，可自定义）下的图片
///   （.jpg/.jpeg/.png/.bmp），文件名主名与 users.json 人员姓名匹配，生成绑定关系；
///   「默认」命名的图片识别为默认壁纸（缺失回退用），不计入多余图片；
/// - SetWallpaper：调用 Windows API（SystemParametersInfo SPI_SETDESKWALLPAPER）
///   设置桌面壁纸，并按设置项应用填充方式。
/// </summary>
public sealed class WallpaperService : IWallpaperService
{
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };

    /// <summary>默认壁纸文件名主名（如 默认.jpg / 默认.png），用于缺失回退。</summary>
    public const string DefaultWallpaperName = "默认";

    // ---- Windows API：桌面壁纸 ----
    private const uint SpiSetDesktopWallpaper = 0x0014;
    private const uint SpiGetDesktopWallpaper = 0x0073;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendChange = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

    private readonly IConfigService _configService;
    private readonly string _wallpapersDir;

    public WallpaperService(IConfigService configService)
        : this(configService, configService.GetSettings().WallpapersDir)
    {
    }

    /// <summary>便于测试：可指定壁纸目录。</summary>
    public WallpaperService(IConfigService configService, string wallpapersDir)
    {
        _configService = configService;
        _wallpapersDir = wallpapersDir;
    }

    public WallpaperScanResult Scan()
    {
        var result = new WallpaperScanResult();
        if (!Directory.Exists(_wallpapersDir))
        {
            Log.Warning("壁纸目录不存在：{Dir}", _wallpapersDir);
            return result;
        }

        var users = _configService.GetUsers().Users.OrderBy(u => u.Order).ToList();

        // 目录内图片 → 文件名主名（忽略大小写）→ 完整路径
        var imageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_wallpapersDir))
        {
            var extension = Path.GetExtension(file);
            if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            imageMap[Path.GetFileNameWithoutExtension(file)] = file;
        }

        // 识别默认壁纸（缺失回退用），不计入多余图片
        if (imageMap.TryGetValue(DefaultWallpaperName, out var defaultImage))
        {
            result.DefaultWallpaper = defaultImage;
        }

        var matchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            if (imageMap.TryGetValue(user.Name, out var imagePath))
            {
                result.Bindings.Add(new WallpaperEntry { Name = user.Name, ImagePath = imagePath });
                matchedNames.Add(user.Name);
            }
            else
            {
                result.MissingNames.Add(user.Name);
            }
        }

        foreach (var (name, path) in imageMap)
        {
            if (!matchedNames.Contains(name) && !string.Equals(name, DefaultWallpaperName, StringComparison.OrdinalIgnoreCase))
            {
                result.OrphanFiles.Add(path);
            }
        }

        Log.Information(
            "壁纸扫描完成：{Dir}，共 {Total} 人，已绑定 {Bound}，缺失 {Missing}，多余图片 {Orphan}，默认壁纸 {Default}",
            _wallpapersDir,
            result.Bindings.Count + result.MissingNames.Count,
            result.Bindings.Count,
            result.MissingNames.Count,
            result.OrphanFiles.Count,
            result.DefaultWallpaper is null ? "无" : Path.GetFileName(result.DefaultWallpaper));
        return result;
    }

    /// <summary>
    /// 通过 Windows API（SPI_SETDESKWALLPAPER）将指定图片设置为桌面壁纸，
    /// 并应用 settings.json 中的填充方式（Fit/Stretch/Center/Tile）。
    /// 支持 .jpg/.jpeg/.png/.bmp；设置成功后立即生效并持久化（SPIF_UPDATEINIFILE）。
    /// </summary>
    public void SetWallpaper(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("壁纸路径不能为空", nameof(imagePath));
        }

        var fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("壁纸图片不存在", fullPath);
        }

        // 按设置应用填充方式（写注册表，随 SPI_SETDESKWALLPAPER 生效）
        try
        {
            ApplyWallpaperStyle(_configService.GetSettings().WallpaperStyle);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "应用壁纸填充方式失败，继续设置壁纸");
        }

        TryClearWallpaperCache();
        var ok = SystemParametersInfo(SpiSetDesktopWallpaper, 0, fullPath, SpifUpdateIniFile | SpifSendChange);
        if (!ok)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"设置桌面壁纸失败（Win32 错误码 {errorCode}）");
        }

        Log.Information("桌面壁纸已设置为：{Path}", fullPath);
    }

    /// <summary>清除桌面壁纸缓存：同名文件内容替换后，不清理缓存会导致设置不生效。</summary>
    private static void TryClearWallpaperCache()
    {
        try
        {
            var themesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes");
            var cachedWallpaper = Path.Combine(themesDir, "TranscodedWallpaper");
            if (File.Exists(cachedWallpaper))
            {
                File.Delete(cachedWallpaper);
            }

            var cachedFilesDir = Path.Combine(themesDir, "CachedFiles");
            if (Directory.Exists(cachedFilesDir))
            {
                foreach (var file in Directory.EnumerateFiles(cachedFilesDir))
                {
                    File.Delete(file);
                }
            }

            Log.Information("已清除壁纸缓存（同名替换后强制刷新）");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清除壁纸缓存失败（不影响设置壁纸）");
        }
    }
    /// <summary>读取当前桌面壁纸路径（供恢复/验证使用）。</summary>
    public static string GetCurrentWallpaper()
    {
        var buffer = new StringBuilder(1024);
        SystemParametersInfo(SpiGetDesktopWallpaper, (uint)buffer.Capacity, buffer, 0);
        return buffer.ToString();
    }

    /// <summary>按样式名称写入桌面壁纸样式注册表（HKCU\Control Panel\Desktop）。</summary>
    private static void ApplyWallpaperStyle(string style)
    {
        var (styleValue, tileValue) = style switch
        {
            "Stretch" => (6, 0),   // 拉伸
            "Center" => (0, 0),    // 居中
            "Tile" => (0, 1),      // 平铺
            _ => (10, 0),          // 适应（Fit，默认）
        };

        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
        key.SetValue("WallpaperStyle", styleValue.ToString(), RegistryValueKind.String);
        key.SetValue("TileWallpaper", tileValue.ToString(), RegistryValueKind.String);
        Log.Information("壁纸填充方式已设置：{Style}", style);
    }
}

