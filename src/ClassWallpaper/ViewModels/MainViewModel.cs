using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using ClassWallpaper.Models;
using ClassWallpaper.Services;
using ClassWallpaper.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClassWallpaper.ViewModels;

/// <summary>
/// 主窗口视图模型（概览页）：
/// 今日轮值大字卡片、数据统计、系统状态、待办提醒、快捷操作。
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly IConfigService _configService;
    private readonly IWallpaperService _wallpaperService;
    private readonly ISchedulerService _schedulerService;
    private readonly IAutoStartService _autoStartService;

    /// <summary>应用名称。</summary>
    public string AppName => AppInfo.AppName;

    /// <summary>程序集版本。</summary>
    public string Version => AppInfo.Version;

    /// <summary>主配置文件路径（D 盘）。</summary>
    public string ConfigPath => PathHelper.ConfigFilePath;

    /// <summary>壁纸目录（取自设置）。</summary>
    public string WallpapersPath => _config.WallpapersDir;

    /// <summary>日志目录（D 盘）。</summary>
    public string LogsPath => PathHelper.LogsDir;

    // ---- 今日轮值 ----

    private string _todayTitle = string.Empty;

    /// <summary>今日轮值标题（如"今天 · 8月5日 星期三"）。</summary>
    public string TodayTitle
    {
        get => _todayTitle;
        private set => SetProperty(ref _todayTitle, value);
    }

    private string _todayName = string.Empty;

    /// <summary>今日轮值人员（无排班时提示）。</summary>
    public string TodayName
    {
        get => _todayName;
        private set => SetProperty(ref _todayName, value);
    }

    private string _todaySubInfo = string.Empty;

    /// <summary>今日轮值附加信息（壁纸状态等）。</summary>
    public string TodaySubInfo
    {
        get => _todaySubInfo;
        private set => SetProperty(ref _todaySubInfo, value);
    }

    // ---- 数据统计 ----

    private int _userCount;
    public int UserCount { get => _userCount; private set => SetProperty(ref _userCount, value); }

    private int _boundCount;
    public int BoundCount { get => _boundCount; private set => SetProperty(ref _boundCount, value); }

    private int _missingCount;
    public int MissingCount { get => _missingCount; private set => SetProperty(ref _missingCount, value); }

    private int _scheduleCount;
    public int ScheduleCount { get => _scheduleCount; private set => SetProperty(ref _scheduleCount, value); }

    // ---- 系统状态 ----

    private bool _isAutoStart;
    public bool IsAutoStart { get => _isAutoStart; private set => SetProperty(ref _isAutoStart, value); }

    private string _rotationIntervalText = string.Empty;
    public string RotationIntervalText { get => _rotationIntervalText; private set => SetProperty(ref _rotationIntervalText, value); }

    private string _lastAppliedText = string.Empty;
    public string LastAppliedText { get => _lastAppliedText; private set => SetProperty(ref _lastAppliedText, value); }

    private bool _hasDefaultWallpaper;
    public bool HasDefaultWallpaper { get => _hasDefaultWallpaper; private set => SetProperty(ref _hasDefaultWallpaper, value); }

    /// <summary>待办提醒列表。</summary>
    public ObservableCollection<string> Warnings { get; } = new();

    private string _statusMessage = "就绪";

    /// <summary>当前状态信息。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand ApplyNowCommand { get; }
    public IRelayCommand OpenWallpapersCommand { get; }
    public IRelayCommand RefreshCommand { get; }

    public MainViewModel(
        AppConfig settings,
        IConfigService configService,
        IWallpaperService wallpaperService,
        ISchedulerService schedulerService,
        IAutoStartService autoStartService)
    {
        _config = settings;
        _configService = configService;
        _wallpaperService = wallpaperService;
        _schedulerService = schedulerService;
        _autoStartService = autoStartService;

        ApplyNowCommand = new RelayCommand(ApplyNow);
        OpenWallpapersCommand = new RelayCommand(OpenWallpapers);
        RefreshCommand = new RelayCommand(LoadOverview);

        LoadOverview();
    }

    /// <summary>聚合概览数据（今日轮值/统计/系统状态/待办提醒）。</summary>
    private void LoadOverview()
    {
        try
        {
            var users = _configService.GetUsers().Users;
            var scan = _wallpaperService.Scan();
            var scheduleItems = _configService.GetSchedule().Items;

            UserCount = users.Count;
            BoundCount = scan.Bindings.Count;
            MissingCount = scan.MissingNames.Count;
            ScheduleCount = scheduleItems.Count;
            HasDefaultWallpaper = scan.DefaultWallpaper is not null;

            // 今日轮值
            var today = DateTime.Today;
            TodayTitle = $"今天 · {today:MM月dd日 dddd}";
            var todayItem = scheduleItems.OrderBy(i => i.Date).FirstOrDefault(i => i.Date.Date == today);
            if (todayItem is not null)
            {
                TodayName = todayItem.Name;
                var hasImage = scan.Bindings.Any(b => b.Name == todayItem.Name);
                TodaySubInfo = hasImage
                    ? "壁纸已上传，将按排班自动切换"
                    : scan.DefaultWallpaper is not null
                        ? "壁纸未上传，届时将回退默认壁纸"
                        : "壁纸未上传且无默认壁纸，需补充";
            }
            else
            {
                TodayName = "今日无排班";
                TodaySubInfo = scheduleItems.Count == 0
                    ? "请到「排班计划」页生成排班"
                    : "今天不是排班日";
            }

            // 系统状态
            RotationIntervalText = $"每 {_config.RotationIntervalHours:0.#} 小时检查";
            LastAppliedText = _config.LastAppliedDate?.ToString("yyyy-MM-dd") ?? "暂无";
            try
            {
                IsAutoStart = _autoStartService.IsEnabled();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "读取开机自启状态失败");
            }

            // 待办提醒
            Warnings.Clear();
            if (ScheduleCount == 0)
            {
                Warnings.Add("排班为空：请到「排班计划」生成本周排班");
            }

            if (MissingCount > 0)
            {
                var example = scan.MissingNames.OrderBy(n => n).First();
                Warnings.Add($"{MissingCount} 人未上传壁纸（如「{example}」），请将图片命名为 姓名.jpg 放入壁纸目录");
            }

            if (!HasDefaultWallpaper)
            {
                Warnings.Add("未设置默认壁纸（默认.jpg）：人员缺图时无法回退");
            }

            StatusMessage = "概览已更新";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "概览数据加载失败");
            StatusMessage = $"概览加载失败:{ex.Message}";
        }
    }

    /// <summary>立即执行排班检查并换壁纸（与托盘「立即换壁纸」一致）。</summary>
    private void ApplyNow()
    {
        try
        {
            var result = _schedulerService.CheckAndApply();
            StatusMessage = result.Message;
            LoadOverview();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "立即换壁纸失败");
            StatusMessage = $"换壁纸失败:{ex.Message}";
        }
    }

    /// <summary>打开壁纸目录。</summary>
    private void OpenWallpapers()
    {
        Directory.CreateDirectory(_config.WallpapersDir);
        Process.Start(new ProcessStartInfo("explorer.exe", _config.WallpapersDir)
        {
            UseShellExecute = true,
        });
    }
}
