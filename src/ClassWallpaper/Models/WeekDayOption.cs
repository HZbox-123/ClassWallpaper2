using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassWallpaper.Models;

/// <summary>排班星期勾选项（界面 CheckBox 绑定）。</summary>
public sealed class WeekDayOption : ObservableObject
{
    public DayOfWeek Day { get; init; }

    /// <summary>显示名（如"星期一"）。</summary>
    public string Label { get; init; } = string.Empty;

    private bool _isChecked;

    /// <summary>是否勾选。</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}
