namespace ClassWallpaper.Models;

/// <summary>壁纸切换计划（schedule.json）：生成的排班条目 + 生成参数（供界面回显与重新生成）。</summary>
public sealed class ScheduleConfig
{
    /// <summary>配置结构版本号。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>排班条目（按日期排序）。</summary>
    public List<ScheduleItem> Items { get; set; } = new();

    /// <summary>生成参数：每周排班的星期几（DayOfWeek 数值，如 1=周一、4=周四）。</summary>
    public List<int> WeekDays { get; set; } = new();

    /// <summary>生成参数：开始日期。</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>生成参数：结束日期。</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>生成参数：跳过的节假日日期。</summary>
    public List<DateTime> Holidays { get; set; } = new();
}
