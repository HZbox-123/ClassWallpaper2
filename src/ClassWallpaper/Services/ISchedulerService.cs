using ClassWallpaper.Models;

namespace ClassWallpaper.Services;

/// <summary>排班执行服务：程序启动时检查排班，今天有排班则换壁纸，错过日期则补执行最近一次。</summary>
public interface ISchedulerService
{
    /// <summary>以今天为基准执行检查（等价于 CheckAndApply(DateTime.Today)）。</summary>
    SchedulerApplyResult CheckAndApply();

    /// <summary>
    /// 排班执行逻辑：
    /// 1) 今天有排班条目 → 设置该人员壁纸；
    /// 2) 否则若有「晚于上次执行且早于今天」的条目 → 补执行最近一次；
    /// 3) 执行后（含壁纸缺失）将 LastAppliedDate 更新为该条目日期，避免重复补执行。
    /// </summary>
    SchedulerApplyResult CheckAndApply(DateTime today);
}
