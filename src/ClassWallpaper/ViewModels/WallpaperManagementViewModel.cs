using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassWallpaper.Models;
using ClassWallpaper.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClassWallpaper.ViewModels;

/// <summary>
/// 壁纸管理视图模型：扫描结果列表（绑定/缺失）、选中预览、应用到桌面、刷新。
/// </summary>
public sealed class WallpaperManagementViewModel : ObservableObject
{
    private readonly IWallpaperService _wallpaperService;

    /// <summary>壁纸绑定列表（已绑定在前，缺失在后，均按人员排序号）。</summary>
    public ObservableCollection<WallpaperEntry> Entries { get; } = new();

    private WallpaperEntry? _selectedEntry;

    /// <summary>当前选中条目。</summary>
    public WallpaperEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                ApplyToDesktopCommand.NotifyCanExecuteChanged();
                UpdatePreview();
            }
        }
    }

    private ImageSource? _previewImage;

    /// <summary>预览图（选中人员对应的壁纸）。</summary>
    public ImageSource? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    private string _previewPath = "（未选中人员）";

    /// <summary>预览图片路径说明。</summary>
    public string PreviewPath
    {
        get => _previewPath;
        private set => SetProperty(ref _previewPath, value);
    }

    private string _statusMessage = "就绪";

    /// <summary>扫描统计信息。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand ApplyToDesktopCommand { get; }
    public IRelayCommand RefreshCommand { get; }

    public WallpaperManagementViewModel(IWallpaperService wallpaperService)
    {
        _wallpaperService = wallpaperService;
        ApplyToDesktopCommand = new RelayCommand(ApplyToDesktop, () => SelectedEntry is { HasImage: true });
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    /// <summary>将选中人员的壁纸设置为桌面壁纸。</summary>
    private void ApplyToDesktop()
    {
        if (SelectedEntry is not { HasImage: true } || SelectedEntry.ImagePath is null)
        {
            StatusMessage = "请先选择一个已绑定壁纸的人员";
            return;
        }

        try
        {
            _wallpaperService.SetWallpaper(SelectedEntry.ImagePath);
            StatusMessage = $"已应用「{SelectedEntry.Name}」的壁纸到桌面（{Path.GetFileName(SelectedEntry.ImagePath)}）";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "应用壁纸到桌面失败：{Path}", SelectedEntry.ImagePath);
            StatusMessage = $"应用失败:{ex.Message}";
        }
    }

    private void Refresh()
    {
        WallpaperScanResult result;
        try
        {
            result = _wallpaperService.Scan();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "壁纸扫描失败");
            StatusMessage = $"扫描失败:{ex.Message}";
            return;
        }

        Entries.Clear();
        foreach (var entry in result.Bindings)
        {
            Entries.Add(entry);
        }

        foreach (var name in result.MissingNames)
        {
            Entries.Add(new WallpaperEntry { Name = name });
        }

        var total = result.Bindings.Count + result.MissingNames.Count;
        StatusMessage = $"共 {total} 人，已绑定 {result.Bindings.Count} 人，缺失壁纸 {result.MissingNames.Count} 人，多余图片 {result.OrphanFiles.Count} 张";
        if (result.OrphanFiles.Count > 0)
        {
            StatusMessage += $"（多余:{string.Join("、", result.OrphanFiles.Select(Path.GetFileName))}）";
        }

        UpdatePreview();
    }

    /// <summary>按选中条目生成预览图（失败时清空预览并提示）。</summary>
    private void UpdatePreview()
    {
        if (SelectedEntry is { HasImage: true } && File.Exists(SelectedEntry.ImagePath))
        {
            try
            {
                // 用文件流加载，避免 WPF 对相同 URI 的解码缓存导致内容不刷新
                using var stream = File.OpenRead(SelectedEntry.ImagePath);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewImage = bitmap;
                PreviewPath = SelectedEntry.ImagePath;
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "壁纸预览失败：{Path}", SelectedEntry.ImagePath);
            }
        }

        PreviewImage = null;
        PreviewPath = SelectedEntry is null
            ? "（未选中人员）"
            : $"（{SelectedEntry.Name} 暂无壁纸图片）";
    }
}

