using System.ComponentModel;
using System.Windows;
using ClassWallpaper.ViewModels;

namespace ClassWallpaper.Views;

/// <summary>
/// 主窗口。仅负责视图与 DataContext 装配（由 DI 注入），不承载业务逻辑。
/// 关闭窗口时隐藏到系统托盘（后台运行），退出需通过托盘菜单。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(
        MainViewModel viewModel,
        UserManagementViewModel userManagementViewModel,
        WallpaperManagementViewModel wallpaperManagementViewModel,
        ScheduleManagementViewModel scheduleManagementViewModel,
        SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        UserView.DataContext = userManagementViewModel;
        WallpaperView.DataContext = wallpaperManagementViewModel;
        ScheduleView.DataContext = scheduleManagementViewModel;
        SettingsViewControl.DataContext = settingsViewModel;

        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 后台运行：非退出流程下，关闭 = 隐藏到托盘
        if (!App.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
