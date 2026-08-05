using System.Collections.ObjectModel;
using ClassWallpaper.Models;
using ClassWallpaper.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClassWallpaper.ViewModels;

/// <summary>
/// 人员管理视图模型：人员列表、添加、删除（支持多选批量）、排序（上移/下移）、Excel 导入。
/// 所有变更通过 IUserService 立即保存到 users.json。
/// </summary>
public sealed class UserManagementViewModel : ObservableObject
{
    private readonly IUserService _userService;

    /// <summary>人员列表（按排序号）。</summary>
    public ObservableCollection<UserConfig> Users { get; } = new();

    /// <summary>当前选中的人员（支持多选）。</summary>
    public ObservableCollection<UserConfig> SelectedUsers { get; } = new();

    private string _newUserName = string.Empty;

    /// <summary>新增人员姓名输入。</summary>
    public string NewUserName
    {
        get => _newUserName;
        set => SetProperty(ref _newUserName, value);
    }

    private string _newUserOrder = string.Empty;

    /// <summary>新增人员排序号输入（可选，留空追加到末尾）。</summary>
    public string NewUserOrder
    {
        get => _newUserOrder;
        set => SetProperty(ref _newUserOrder, value);
    }

    private string _statusMessage = "就绪";

    /// <summary>操作状态提示。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand ImportExcelCommand { get; }

    public UserManagementViewModel(IUserService userService)
    {
        _userService = userService;

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => SelectedUsers.Count > 0);
        MoveUpCommand = new RelayCommand(() => Move(-1), () => SelectedUsers.Count == 1);
        MoveDownCommand = new RelayCommand(() => Move(1), () => SelectedUsers.Count == 1);
        ImportExcelCommand = new RelayCommand(ImportExcel);

        Reload();
    }

    /// <summary>由视图的 SelectionChanged 事件调用，同步多选集合。</summary>
    public void UpdateSelection(IEnumerable<UserConfig> selected)
    {
        SelectedUsers.Clear();
        foreach (var user in selected)
        {
            SelectedUsers.Add(user);
        }

        RemoveCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        StatusMessage = SelectedUsers.Count > 1
            ? $"已选中 {SelectedUsers.Count} 人，可批量删除"
            : $"共 {Users.Count} 人";
    }

    private void Reload()
    {
        Users.Clear();
        foreach (var user in _userService.GetUsers())
        {
            Users.Add(user);
        }

        StatusMessage = $"共 {Users.Count} 人";
    }

    private void Add()
    {
        int? order = null;
        if (int.TryParse(NewUserOrder.Trim(), out var parsed) && parsed > 0)
        {
            order = parsed;
        }

        if (_userService.AddUser(NewUserName, order))
        {
            NewUserName = string.Empty;
            NewUserOrder = string.Empty;
            Reload();
            StatusMessage = "已添加并保存到 users.json";
        }
        else
        {
            StatusMessage = "添加失败：姓名不能为空";
        }
    }

    private void Remove()
    {
        if (SelectedUsers.Count == 0)
        {
            return;
        }

        var names = string.Join("、", SelectedUsers.Select(u => u.Name));
        var count = SelectedUsers.Count;
        var message = count == 1
            ? $"确定删除「{names}」？"
            : $"确定删除选中的 {count} 人？\n{names}";

        if (System.Windows.MessageBox.Show(message, "删除确认",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var user in SelectedUsers.ToList())
        {
            _userService.RemoveUser(user);
        }

        Reload();
        StatusMessage = $"已删除 {count} 人并保存到 users.json";
    }

    private void Move(int delta)
    {
        if (SelectedUsers.Count != 1)
        {
            return; // 多选时不支持移动
        }

        var user = SelectedUsers[0];
        _userService.MoveUser(user, delta);
        Reload();
        StatusMessage = $"已移动「{user.Name}」并保存到 users.json";
    }

    private void ImportExcel()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择班级名单 Excel（.xlsx：A 列姓名，B 列排序）",
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var count = _userService.ImportFromExcel(dialog.FileName);
            Reload();
            StatusMessage = count > 0
                ? $"已从 Excel 导入 {count} 人并保存到 users.json"
                : "Excel 中未读取到有效姓名（请确认 A 列为姓名）";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Excel 导入失败：{Path}", dialog.FileName);
            StatusMessage = $"导入失败:{ex.Message}";
        }
    }
}
