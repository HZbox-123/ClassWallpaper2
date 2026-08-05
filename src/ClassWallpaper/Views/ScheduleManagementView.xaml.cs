using UserControl = System.Windows.Controls.UserControl;

namespace ClassWallpaper.Views;

/// <summary>
/// 排班管理视图。DataContext 由 MainWindow 装配（ScheduleManagementViewModel）。
/// </summary>
public partial class ScheduleManagementView : UserControl
{
    public ScheduleManagementView()
    {
        InitializeComponent();
    }
}
