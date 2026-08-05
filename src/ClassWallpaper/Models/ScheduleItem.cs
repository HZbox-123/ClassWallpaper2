using System.Text.Json.Serialization;

namespace ClassWallpaper.Models;

/// <summary>排班计划条目（schedule.json 中的一条）：某日期对应一名人员。</summary>
public sealed class ScheduleItem
{
    /// <summary>排班日期。</summary>
    public DateTime Date { get; set; }

    /// <summary>当日人员姓名（壁纸取"姓名.jpg"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>星期显示文本（中文，仅供界面，不写入 JSON）。</summary>
    [JsonIgnore]
    public string WeekdayText => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Thursday => "星期四",
        _ => Date.DayOfWeek.ToString(),
    };
}
