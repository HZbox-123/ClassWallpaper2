using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using ClassWallpaper.Utils;
using Serilog;
using Forms = System.Windows.Forms;

namespace ClassWallpaper.Services;

/// <summary>
/// 系统托盘服务：后台运行（关闭窗口不退出程序），
/// 右键菜单：打开 / 立即换壁纸 / 打开壁纸目录 / 退出。
/// 基于 WinForms NotifyIcon（系统内置，无额外 NuGet 依赖），图标取自嵌入资源。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Window _mainWindow;
    private readonly ISchedulerService _schedulerService;
    private readonly IConfigService _configService;
    private Forms.NotifyIcon? _notifyIcon;

    public TrayIconService(
        Window mainWindow,
        ISchedulerService schedulerService,
        IConfigService configService)
    {
        _mainWindow = mainWindow;
        _schedulerService = schedulerService;
        _configService = configService;
    }

    /// <summary>创建并显示托盘图标与右键菜单。</summary>
    public void Show()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开", null, (_, _) => ShowMainWindow());
        menu.Items.Add("立即换壁纸", null, (_, _) => ApplyNow());
        menu.Items.Add("打开壁纸目录", null, (_, _) => OpenWallpapersFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "ClassWallpaper 班级壁纸",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ShowMainWindow();
            }
        };
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    /// <summary>显示托盘气泡提示（供排班检查/定时器缺失提醒调用）。</summary>
    public void ShowBalloon(string title, string text)
        => _notifyIcon?.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Warning);

    /// <summary>从嵌入资源加载应用图标（失败时回退系统图标）。</summary>
    private static Icon LoadTrayIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("ClassWallpaper.Assets.ClassWallpaper.ico");
            if (stream is not null)
            {
                return new Icon(stream);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载托盘图标失败，使用系统默认图标");
        }

        return SystemIcons.Application;
    }

    // ---------- 菜单动作 ----------

    /// <summary>显示并激活主窗口（最小化时还原）。</summary>
    private void ShowMainWindow()
    {
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    /// <summary>立即执行排班检查并换壁纸（与启动逻辑一致），结果以气泡提示。</summary>
    private void ApplyNow()
    {
        try
        {
            var result = _schedulerService.CheckAndApply();
            Log.Information("托盘菜单：立即换壁纸 → {Message}", result.Message);
            ShowBalloon(result.Applied ? "壁纸已更换" : "未更换壁纸", result.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "托盘菜单：立即换壁纸失败");
            ShowBalloon("换壁纸失败", ex.Message);
        }
    }

    /// <summary>打开壁纸目录（资源管理器，目录取自设置）。</summary>
    private void OpenWallpapersFolder()
    {
        var dir = _configService.GetSettings().WallpapersDir;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", dir)
        {
            UseShellExecute = true,
        });
    }

    /// <summary>退出程序（通知 App 放行窗口关闭并关闭应用）。</summary>
    private void Exit()
    {
        Log.Information("托盘菜单：退出程序");
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }

        App.RequestExit();
    }
}
