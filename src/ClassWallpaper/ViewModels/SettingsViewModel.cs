using System.Collections.ObjectModel;
using ClassWallpaper.Models;
using ClassWallpaper.Services;
using ClassWallpaper.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClassWallpaper.ViewModels;

/// <summary>
/// 设置视图模型：settings.json 可视化编辑
/// （日志保留天数、壁纸填充方式、启动显示窗口、定时检查间隔、壁纸目录、开机自启）。
/// 保存后立即写盘，除定时器间隔需重启外均即时生效。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IAutoStartService _autoStartService;
    private readonly AppConfig _config;

    /// <summary>日志保留天数选项。</summary>
    public ObservableCollection<int> RetentionOptions { get; } = new() { 7, 15, 30, 60, 90 };

    /// <summary>壁纸填充方式选项。</summary>
    public ObservableCollection<string> WallpaperStyleOptions { get; } = new()
    {
        "Fit", "Stretch", "Center", "Tile",
    };

    /// <summary>定时检查间隔选项（小时）。</summary>
    public ObservableCollection<double> IntervalOptions { get; } = new() { 0.25, 0.5, 1, 2, 6, 12 };

    private int _logRetentionDays = 30;

    /// <summary>日志保留天数。</summary>
    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set => SetProperty(ref _logRetentionDays, value);
    }

    private string _wallpaperStyle = "Fit";

    /// <summary>壁纸填充方式。</summary>
    public string WallpaperStyle
    {
        get => _wallpaperStyle;
        set => SetProperty(ref _wallpaperStyle, value);
    }

    private bool _showMainWindowOnStartup = true;

    /// <summary>启动时是否显示主窗口。</summary>
    public bool ShowMainWindowOnStartup
    {
        get => _showMainWindowOnStartup;
        set => SetProperty(ref _showMainWindowOnStartup, value);
    }

    private double _rotationIntervalHours = 1;

    /// <summary>定时检查间隔（小时）。</summary>
    public double RotationIntervalHours
    {
        get => _rotationIntervalHours;
        set => SetProperty(ref _rotationIntervalHours, value);
    }

    private string _wallpapersDir = string.Empty;

    /// <summary>当前数据根目录（自动探测，支持自定义安装）。</summary>
    public string DataDir => PathHelper.AppRoot;

    /// <summary>壁纸目录。</summary>
    public string WallpapersDir
    {
        get => _wallpapersDir;
        set => SetProperty(ref _wallpapersDir, value);
    }

    private bool _isAutoStart;

    /// <summary>是否已开启开机自启。</summary>
    public bool IsAutoStart
    {
        get => _isAutoStart;
        private set
        {
            if (SetProperty(ref _isAutoStart, value))
            {
                OnPropertyChanged(nameof(AutoStartButtonText));
            }
        }
    }

    /// <summary>自启开关按钮文本。</summary>
    public string AutoStartButtonText => IsAutoStart ? "关闭开机自启" : "开启开机自启";

    private string _statusMessage = "就绪";

    /// <summary>操作状态提示。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand BrowseWallpapersDirCommand { get; }
    public IRelayCommand ToggleAutoStartCommand { get; }

    public SettingsViewModel(IConfigService configService, IAutoStartService autoStartService, AppConfig config)
    {
        _configService = configService;
        _autoStartService = autoStartService;
        _config = config;

        SaveCommand = new RelayCommand(Save);
        BrowseWallpapersDirCommand = new RelayCommand(BrowseWallpapersDir);
        ToggleAutoStartCommand = new RelayCommand(ToggleAutoStart);

        // 从当前配置回显
        LogRetentionDays = _config.LogRetentionDays;
        WallpaperStyle = _config.WallpaperStyle;
        ShowMainWindowOnStartup = _config.ShowMainWindowOnStartup;
        RotationIntervalHours = _config.RotationIntervalHours;
        WallpapersDir = _config.WallpapersDir;

        try
        {
            IsAutoStart = _autoStartService.IsEnabled();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取开机自启状态失败");
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(WallpapersDir))
        {
            StatusMessage = "保存失败：壁纸目录不能为空";
            return;
        }

        try
        {
            _config.LogRetentionDays = LogRetentionDays;
            _config.WallpaperStyle = WallpaperStyle;
            _config.ShowMainWindowOnStartup = ShowMainWindowOnStartup;
            _config.RotationIntervalHours = RotationIntervalHours;
            _config.WallpapersDir = WallpapersDir.Trim();
            _configService.SaveSettings(_config);
            Log.Information("设置已保存：保留日志 {Retention} 天，样式 {Style}，启动显示窗口 {ShowWindow}，检查间隔 {Interval} 小时，壁纸目录 {Dir}",
                LogRetentionDays, WallpaperStyle, ShowMainWindowOnStartup, RotationIntervalHours, WallpapersDir);
            StatusMessage = "设置已保存（定时检查间隔修改后重启应用生效）";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "保存设置失败");
            StatusMessage = $"保存失败:{ex.Message}";
        }
    }

    private void BrowseWallpapersDir()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择壁纸目录",
            SelectedPath = WallpapersDir,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            WallpapersDir = dialog.SelectedPath;
        }
    }

    private void ToggleAutoStart()
    {
        try
        {
            if (IsAutoStart)
            {
                _autoStartService.Disable();
                IsAutoStart = false;
                StatusMessage = "已关闭开机自启";
            }
            else
            {
                _autoStartService.Enable();
                IsAutoStart = true;
                StatusMessage = "已开启开机自启（部署机请在解冻状态设置）";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "切换开机自启失败");
            StatusMessage = $"自启设置失败:{ex.Message}";
        }
    }
}


