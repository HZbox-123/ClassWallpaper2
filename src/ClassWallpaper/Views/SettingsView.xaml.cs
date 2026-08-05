using WpfControls = System.Windows.Controls;

namespace ClassWallpaper.Views;

/// <summary>
/// 设置视图。DataContext 由 MainWindow 装配（SettingsViewModel）。
/// </summary>
public partial class SettingsView : WpfControls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
