using System.Collections.ObjectModel;
using ClassWallpaper.Models;
using ClassWallpaper.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ClassWallpaper.ViewModels;

/// <summary>
/// 排班管理视图模型：
/// - 参数：自定义星期几（勾选）、起止日期、节假日列表；
/// - 生成：按人员顺序循环生成计划（同一周每人最多一次）；
/// - 计划列表可手动编辑（日期/姓名），保存后写回 schedule.json。
/// </summary>
public sealed class ScheduleManagementViewModel : ObservableObject
{
    private readonly IScheduleService _scheduleService;
    private readonly ISchedulerService _schedulerService;

    /// <summary>星期几勾选（周一~周日）。</summary>
    public ObservableCollection<WeekDayOption> WeekDayOptions { get; } = new()
    {
        new() { Day = DayOfWeek.Monday, Label = "星期一" },
        new() { Day = DayOfWeek.Tuesday, Label = "星期二" },
        new() { Day = DayOfWeek.Wednesday, Label = "星期三" },
        new() { Day = DayOfWeek.Thursday, Label = "星期四" },
        new() { Day = DayOfWeek.Friday, Label = "星期五" },
        new() { Day = DayOfWeek.Saturday, Label = "星期六" },
        new() { Day = DayOfWeek.Sunday, Label = "星期日" },
    };

    private DateTime _startDate = DateTime.Today;

    /// <summary>生成开始日期。</summary>
    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    private DateTime _endDate = DateTime.Today.AddDays(60);

    /// <summary>生成结束日期。</summary>
    public DateTime EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    /// <summary>节假日列表（生成时跳过）。</summary>
    public ObservableCollection<DateTime> Holidays { get; } = new();

    private DateTime _newHoliday = DateTime.Today;

    /// <summary>新节假日选择（添加用）。</summary>
    public DateTime NewHoliday
    {
        get => _newHoliday;
        set => SetProperty(ref _newHoliday, value);
    }

    /// <summary>排班列表（按日期排序）。</summary>
    public ObservableCollection<ScheduleItem> Items { get; } = new();

    private ScheduleItem? _selectedItem;

    /// <summary>当前选中条目。</summary>
    public ScheduleItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private string _statusMessage = "就绪";

    /// <summary>操作状态提示。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand GenerateCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand AddHolidayCommand { get; }
    public IRelayCommand RemoveHolidayCommand { get; }

    public ScheduleManagementViewModel(IScheduleService scheduleService, ISchedulerService schedulerService)
    {
        _scheduleService = scheduleService;
        _schedulerService = schedulerService;

        GenerateCommand = new RelayCommand(Generate);
        SaveCommand = new RelayCommand(Save);
        AddHolidayCommand = new RelayCommand(AddHoliday);
        RemoveHolidayCommand = new RelayCommand(RemoveHoliday, () => SelectedHoliday is not null);

        Reload();
    }

    private DateTime? _selectedHoliday;

    /// <summary>当前选中的节假日（删除用）。</summary>
    public DateTime? SelectedHoliday
    {
        get => _selectedHoliday;
        set
        {
            if (SetProperty(ref _selectedHoliday, value))
            {
                RemoveHolidayCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void Reload()
    {
        var schedule = _scheduleService.GetSchedule();

        // 回显生成参数
        foreach (var option in WeekDayOptions)
        {
            option.IsChecked = schedule.WeekDays.Contains((int)option.Day);
        }

        if (schedule.StartDate is not null)
        {
            StartDate = schedule.StartDate.Value;
        }

        if (schedule.EndDate is not null)
        {
            EndDate = schedule.EndDate.Value;
        }

        Holidays.Clear();
        foreach (var holiday in schedule.Holidays.OrderBy(h => h))
        {
            Holidays.Add(holiday);
        }

        Items.Clear();
        foreach (var item in schedule.Items)
        {
            Items.Add(item);
        }

        StatusMessage = Items.Count > 0
            ? $"当前计划 {Items.Count} 天（{Items[0].Date:yyyy-MM-dd} ~ {Items[^1].Date:yyyy-MM-dd}），可直接编辑后保存"
            : "暂无计划：勾选星期、设置起止日期后点击「生成计划」";
    }

    private void Generate()
    {
        var weekDays = WeekDayOptions.Where(o => o.IsChecked).Select(o => o.Day).ToList();
        if (weekDays.Count == 0)
        {
            StatusMessage = "生成失败：请至少勾选一个星期几";
            return;
        }

        if (EndDate < StartDate)
        {
            StatusMessage = "生成失败：结束日期不能早于开始日期";
            return;
        }

        try
        {
            _scheduleService.Generate(StartDate, EndDate, weekDays, Holidays.ToList());
            Reload();

            // 生成后立即执行一次排班检查：若今天在计划内则马上换壁纸，不必等下一个定时周期
            try
            {
                var applied = _schedulerService.CheckAndApply();
                StatusMessage = applied.Applied
                    ? $"已生成 {Items.Count} 天计划，并已应用今日壁纸（{applied.Message}）"
                    : $"已生成 {Items.Count} 天计划（{applied.Message}）";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "生成后立即应用壁纸失败");
                StatusMessage = $"已生成 {Items.Count} 天计划（今日应用失败:{ex.Message}）";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "排班生成失败");
            StatusMessage = $"生成失败:{ex.Message}";
        }
    }

    private void Save()
    {
        try
        {
            var schedule = new ScheduleConfig
            {
                Items = Items.ToList(),
                WeekDays = WeekDayOptions.Where(o => o.IsChecked).Select(o => (int)o.Day).ToList(),
                StartDate = StartDate.Date,
                EndDate = EndDate.Date,
                Holidays = Holidays.OrderBy(h => h).ToList(),
            };
            _scheduleService.Save(schedule);
            Reload();
            StatusMessage = $"已保存 {Items.Count} 天计划到 schedule.json";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "排班保存失败");
            StatusMessage = $"保存失败:{ex.Message}";
        }
    }

    private void AddHoliday()
    {
        var date = NewHoliday.Date;
        if (Holidays.Contains(date))
        {
            StatusMessage = $"节假日已存在:{date:yyyy-MM-dd}";
            return;
        }

        Holidays.Add(date);
        StatusMessage = $"已添加节假日:{date:yyyy-MM-dd}（点「保存修改」或重新生成后生效）";
    }

    private void RemoveHoliday()
    {
        if (SelectedHoliday is null)
        {
            return;
        }

        Holidays.Remove(SelectedHoliday.Value);
        StatusMessage = "已删除该节假日（点「保存修改」或重新生成后生效）";
    }
}

