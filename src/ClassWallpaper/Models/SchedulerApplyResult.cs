namespace ClassWallpaper.Models;

/// <summary>排班执行结果（SchedulerService.CheckAndApply 返回）。</summary>
public sealed class SchedulerApplyResult
{
    /// <summary>是否已实际执行（设置壁纸或跳过但标记日期）。</summary>
    public bool Applied { get; init; }

    /// <summary>结果说明（日志/界面展示）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>执行的排班日期（未执行时为 null）。</summary>
    public DateTime? Date { get; init; }

    /// <summary>执行的人员姓名。</summary>
    public string? Name { get; init; }

    /// <summary>实际设置的壁纸路径（含回退默认壁纸时为其路径）。</summary>
    public string? WallpaperPath { get; init; }

    /// <summary>该人员壁纸是否缺失（含已回退默认壁纸的场景）。</summary>
    public bool MissingWallpaper { get; init; }

    /// <summary>是否应弹气泡提醒（缺失 && 当天首次，SchedulerService 已按天节流）。</summary>
    public bool ShouldRemind { get; init; }

    /// <summary>构造一个正常执行的结果。</summary>
    public static SchedulerApplyResult AppliedResult(DateTime date, string name, string wallpaperPath)
        => new()
        {
            Applied = true,
            Date = date,
            Name = name,
            WallpaperPath = wallpaperPath,
            Message = $"已应用壁纸：{date:yyyy-MM-dd} {name}（{wallpaperPath}）",
        };

    /// <summary>构造一个跳过结果（未执行壁纸切换）。</summary>
    public static SchedulerApplyResult Skipped(string message)
        => new() { Message = message };
}
