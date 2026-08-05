using ClassWallpaper.Models;
using ClassWallpaper.ViewModels;
using WpfControls = System.Windows.Controls;

namespace ClassWallpaper.Views;

/// <summary>
/// 人员管理视图。DataContext 由 MainWindow 装配（UserManagementViewModel）。
/// 多选状态通过 SelectionChanged 事件同步到 ViewModel 的 SelectedUsers。
/// </summary>
public partial class UserManagementView : WpfControls.UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    private void UserList_SelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
    {
        if (DataContext is UserManagementViewModel viewModel)
        {
            viewModel.UpdateSelection(UserList.SelectedItems.Cast<UserConfig>());
        }
    }
}
