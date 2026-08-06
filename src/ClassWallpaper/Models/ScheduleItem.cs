using System.Text.Json.Serialization;

namespace ClassWallpaper.Models;

/// <summary>排班计划条目（schedule.json 中的一条）：一个连续区间对应一名人员。</summary>
public sealed class ScheduleItem
{
    /// <summary>区间开始日期（排班日）。</summary>
    public DateTime Date { get; set; }

    /// <summary>区间结束日期（下一个排班日前一天；最后一段为计划结束日期；旧数据可能为空）。</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>区间人员姓名（壁纸取"姓名.jpg"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>星期显示文本（开始日期，仅供界面，不写入 JSON）。</summary>
    [JsonIgnore]
    public string WeekdayText => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Thursday => "星期四",
        _ => Date.DayOfWeek.ToString(),
    };

    /// <summary>实际结束日期（EndDate 为空时取开始日期，兼容旧数据）。</summary>
    [JsonIgnore]
    public DateTime EndDateOrDate => EndDate ?? Date;
}
