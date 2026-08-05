using UserControl = System.Windows.Controls.UserControl;

namespace ClassWallpaper.Views;

/// <summary>
/// 壁纸管理视图。DataContext 由 MainWindow 装配（WallpaperManagementViewModel）。
/// </summary>
public partial class WallpaperManagementView : UserControl
{
    public WallpaperManagementView()
    {
        InitializeComponent();
    }
}
