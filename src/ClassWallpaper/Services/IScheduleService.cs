using ClassWallpaper.Models;

namespace ClassWallpaper.Services;

/// <summary>排班服务：按自定义星期/起止日期/节假日生成计划（人员顺序循环），支持手动修改后保存。</summary>
public interface IScheduleService
{
    /// <summary>获取当前排班计划（按日期排序）。</summary>
    ScheduleConfig GetSchedule();

    /// <summary>
    /// 生成排班：
    /// - 日期 = startDate ~ endDate 内匹配 weekDays 的星期，且不在 holidays 中；
    /// - 人员按 users.json 排序顺序循环分配，同一周内每人最多一次（人数不足时放宽）；
    /// - 生成参数一并保存到 schedule.json（供界面回显）。
    /// </summary>
    void Generate(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<DayOfWeek> weekDays,
        IReadOnlyCollection<DateTime> holidays);

    /// <summary>保存手动修改后的计划（含界面参数）。</summary>
    void Save(ScheduleConfig schedule);
}
