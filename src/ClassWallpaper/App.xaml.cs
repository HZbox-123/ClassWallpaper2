using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using ClassWallpaper.Models;
using ClassWallpaper.Services;
using ClassWallpaper.Utils;
using ClassWallpaper.ViewModels;
using ClassWallpaper.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ClassWallpaper;

/// <summary>
/// 应用组合根：负责依赖注入注册、日志与配置初始化、主窗口启动。
/// 业务逻辑不在此处，也不写在 MainWindow 中。
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>单实例互斥体名称（同一用户会话内唯一）。</summary>
    private const string SingleInstanceMutexName = @"Local\ClassWallpaper_SingleInstance";

    private ServiceProvider? _serviceProvider;
    private TrayIconService? _trayIconService;
    private System.Threading.Timer? _rotationTimer;
    private Mutex? _singleInstanceMutex;

    /// <summary>是否正在退出（托盘「退出」触发；放行窗口关闭）。</summary>
    public static bool IsExiting { get; private set; }

    /// <summary>请求退出：放行窗口关闭并关闭应用（供托盘菜单调用）。</summary>
    public static void RequestExit()
    {
        IsExiting = true;
        Current.Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例保护：已有实例时激活其窗口并退出当前进程
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            Log.Information("检测到已存在运行实例，激活已有窗口后退出");
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        // 全局未处理异常统一记录日志，避免静默崩溃
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "UI 线程未处理异常");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "非 UI 线程未处理异常");
        };

        try
        {
            // 1) 校验并创建 D 盘基础目录（配置/日志；壁纸目录按配置单独创建）
            PathHelper.EnsureDirectories();

            // 2) 初始化日志（Serilog，写入 D:\ClassWallpaper\Logs）
            LogService.Initialize();
            Log.Information("应用启动，版本 {Version}", AppInfo.Version);

            // 3) 注册依赖并加载配置（首次运行自动生成默认配置文件）
            var services = new ServiceCollection();
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton(sp => sp.GetRequiredService<IConfigService>().Load());
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IWallpaperService, WallpaperService>();
            services.AddSingleton<IScheduleService, ScheduleService>();
            services.AddSingleton<ISchedulerService, SchedulerService>();
            services.AddSingleton<IAutoStartService, AutoStartService>();
            services.AddSingleton<TrayIconService>();
            services.AddSingleton<System.Windows.Window>(sp => sp.GetRequiredService<MainWindow>());
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<UserManagementViewModel>();
            services.AddSingleton<WallpaperManagementViewModel>();
            services.AddSingleton<ScheduleManagementViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainWindow>();
            _serviceProvider = services.BuildServiceProvider();

            var config = _serviceProvider.GetRequiredService<AppConfig>();
            Log.Information("配置加载完成：{ConfigPath}（SchemaVersion={SchemaVersion}）",
                PathHelper.ConfigFilePath, config.SchemaVersion);

            // 4) 创建壁纸目录（可自定义路径；失败时回退默认目录，避免启动崩溃）
            try
            {
                Directory.CreateDirectory(config.WallpapersDir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "壁纸目录创建失败，回退默认目录");
                config.WallpapersDir = PathHelper.WallpapersDir;
                Directory.CreateDirectory(config.WallpapersDir);
            }
            Log.Information("壁纸目录：{Dir}", config.WallpapersDir);

            // 5) 创建系统托盘（关闭窗口后后台驻留）
            _trayIconService = _serviceProvider.GetRequiredService<TrayIconService>();
            _trayIconService.Show();
            Log.Information("系统托盘已启动");

            // 6) 排班检查：今天有排班则换壁纸，错过日期则补执行；
            //    壁纸缺失时回退默认壁纸，并按天首次弹气泡提醒
            try
            {
                var scheduler = _serviceProvider.GetRequiredService<ISchedulerService>();
                var schedulerResult = scheduler.CheckAndApply();
                Log.Information("排班检查：{Message}", schedulerResult.Message);
                if (schedulerResult.ShouldRemind)
                {
                    _trayIconService?.ShowBalloon("壁纸缺失提醒", schedulerResult.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "排班检查/执行异常");
            }

            // 7) 显示主窗口：设置允许 且 非静默启动（开机自启带 -silent 参数 → 只驻留托盘不弹界面）
            var silentStart = e.Args.Any(a => string.Equals(a, "-silent", StringComparison.OrdinalIgnoreCase));
            if (config.ShowMainWindowOnStartup && !silentStart)
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            else if (silentStart)
            {
                Log.Information("静默启动（-silent）：仅后台托盘运行");
            }

            // 8) 定时换壁纸检查：程序驻留期间跨排班日自动切换
            //    首次 1 分钟后执行（启动时已检查过），间隔取自设置（默认 15 分钟）
            var intervalHours = config.RotationIntervalHours > 0 ? config.RotationIntervalHours : 0.25;
            _rotationTimer = new System.Threading.Timer(
                OnRotationTick,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromHours(intervalHours));
            Log.Information("定时换壁纸检查已启动（每 {Interval} 小时一次）", intervalHours);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            System.Windows.MessageBox.Show(
                $"应用启动失败：{ex.Message}\n\n详细信息请查看日志：{Path.Combine(PathHelper.LogsDir, "ClassWallpaper-.log")}",
                "ClassWallpaper",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>定时检查排班并换壁纸（Timer 回调，与启动检查同一逻辑，自动去重）。</summary>
    private void OnRotationTick(object? state)
    {
        try
        {
            var scheduler = _serviceProvider?.GetRequiredService<ISchedulerService>();
            if (scheduler is null)
            {
                return;
            }

            var result = scheduler.CheckAndApply();
            Log.Information(result.Applied
                ? "定时换壁纸：{Message}"
                : "定时换壁纸检查：{Message}", result.Message);
            if (result.ShouldRemind)
            {
                _trayIconService?.ShowBalloon("壁纸缺失提醒", result.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "定时换壁纸检查异常");
        }
    }

    /// <summary>激活已有实例的主窗口（恢复并置前）。</summary>
    private static void ActivateExistingInstance()
    {
        try
        {
            var hWnd = FindWindow(null, AppInfo.AppName);
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, 9); // SW_RESTORE
                SetForegroundWindow(hWnd);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "激活已有实例窗口失败");
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("应用退出");
        _rotationTimer?.Dispose();
        _trayIconService?.Dispose();
        LogService.Shutdown();
        _serviceProvider?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}



