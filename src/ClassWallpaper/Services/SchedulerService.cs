using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassWallpaper.Models;
using ClassWallpaper.Utils;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 排班执行服务实现：
/// - 今日执行：今天在 schedule.json 中有条目 → 设置该人员壁纸；
/// - 错过补执行：今天无条目时，取「晚于上次执行日期且早于今天」的最近一条补执行；
/// - 壁纸缺失处理：优先回退到默认壁纸（壁纸目录\默认.jpg 等），无默认壁纸则跳过；
/// - 缺失提醒：当天首次检测到缺失时置 ShouldRemind（按天节流，避免每小时骚扰）；
/// - 去重：执行后（无论是否成功）把 LastAppliedDate 更新为该条目日期。
/// 壁纸目录取自设置（AppConfig.WallpapersDir，可自定义）。
/// </summary>
public sealed class SchedulerService : ISchedulerService
{
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };

    /// <summary>默认壁纸文件名主名（如 默认.jpg / 默认.png）。</summary>
    private const string DefaultWallpaperName = "默认";

    private readonly IConfigService _configService;
    private readonly IWallpaperService _wallpaperService;
    private readonly string _wallpapersDir;

    private DateTime _lastRemindDate = DateTime.MinValue;

    public SchedulerService(IConfigService configService, IWallpaperService wallpaperService)
        : this(configService, wallpaperService, configService.GetSettings().WallpapersDir)
    {
    }

    /// <summary>便于测试：可指定壁纸目录。</summary>
    public SchedulerService(
        IConfigService configService,
        IWallpaperService wallpaperService,
        string wallpapersDir)
    {
        _configService = configService;
        _wallpaperService = wallpaperService;
        _wallpapersDir = wallpapersDir;
    }

    public SchedulerApplyResult CheckAndApply() => CheckAndApply(DateTime.Today);

    public SchedulerApplyResult CheckAndApply(DateTime today)
    {
        var settings = _configService.GetSettings();
        var schedule = _configService.GetSchedule();
        var items = schedule.Items.OrderBy(i => i.Date).ToList();
        if (items.Count == 0)
        {
            Log.Information("排班检查：schedule.json 为空，跳过");
            return SchedulerApplyResult.Skipped("排班为空，跳过");
        }

        var todayDate = today.Date;

        // 1) 今天有排班 → 直接执行
        var target = items.FirstOrDefault(i => i.Date.Date == todayDate);

        // 2) 错过补执行：晚于上次执行日期且早于今天，取最近一条
        if (target is null)
        {
            var lastApplied = settings.LastAppliedDate;
            target = items
                .Where(i => i.Date.Date < todayDate
                            && (lastApplied is null || i.Date.Date > lastApplied.Value.Date))
                .OrderByDescending(i => i.Date)
                .FirstOrDefault();

            if (target is not null)
            {
                Log.Information("排班检查：发现错过日期 {Date}（{Name}），补执行", target.Date, target.Name);
            }
            else
            {
                Log.Information(
                    "排班检查：今天无排班，且无错过日期（上次执行 {LastApplied}）",
                    lastApplied?.ToString("yyyy-MM-dd") ?? "无");
                return SchedulerApplyResult.Skipped("今天无排班且无错过日期");
            }
        }

        // 3) 执行：查找该人员的壁纸图片
        var imagePath = FindImage(target.Name);
        if (imagePath is null)
        {
            return HandleMissingWallpaper(settings, target);
        }

        _wallpaperService.SetWallpaper(imagePath);
        MarkApplied(settings, target.Date);
        Log.Information("排班执行完成：{Date} {Name} → {Path}", target.Date, target.Name, imagePath);
        return SchedulerApplyResult.AppliedResult(target.Date, target.Name, imagePath);
    }

    /// <summary>
    /// 人员壁纸缺失：回退默认壁纸（有则设置并标记为已应用），无默认则跳过；
    /// 两者都置 MissingWallpaper，并按天节流返回 ShouldRemind。
    /// </summary>
    private SchedulerApplyResult HandleMissingWallpaper(AppConfig settings, ScheduleItem target)
    {
        var defaultImage = FindImage(DefaultWallpaperName);
        MarkApplied(settings, target.Date); // 缺失也标记，避免反复补执行
        var remind = ShouldRemindOnce();

        if (defaultImage is not null)
        {
            try
            {
                _wallpaperService.SetWallpaper(defaultImage);
                Log.Warning("排班执行：{Date} {Name} 壁纸缺失，已回退默认壁纸 {Default}",
                    target.Date, target.Name, defaultImage);
                return new SchedulerApplyResult
                {
                    Applied = true,
                    Date = target.Date,
                    Name = target.Name,
                    WallpaperPath = defaultImage,
                    MissingWallpaper = true,
                    ShouldRemind = remind,
                    Message = $"「{target.Name}」未上传壁纸，已使用默认壁纸",
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "回退默认壁纸失败：{Default}", defaultImage);
            }
        }

        Log.Warning("排班执行：{Date} {Name} 壁纸缺失且无默认壁纸，跳过换壁纸",
            target.Date, target.Name);
        return new SchedulerApplyResult
        {
            Applied = false,
            Date = target.Date,
            Name = target.Name,
            MissingWallpaper = true,
            ShouldRemind = remind,
            Message = $"「{target.Name}」未上传壁纸，且无默认壁纸，维持当前壁纸",
        };
    }

    /// <summary>壁纸缺失提醒按天节流：当天首次返回 true。</summary>
    private bool ShouldRemindOnce()
    {
        var today = DateTime.Today;
        if (_lastRemindDate == today)
        {
            return false;
        }

        _lastRemindDate = today;
        return true;
    }

    /// <summary>在壁纸目录中按姓名查找图片（支持 jpg/jpeg/png/bmp）。</summary>
    private string? FindImage(string name)
    {
        foreach (var extension in SupportedExtensions)
        {
            var path = Path.Combine(_wallpapersDir, name + extension);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>更新上次执行日期并保存（壁纸缺失也标记，避免反复补执行）。</summary>
    private void MarkApplied(AppConfig settings, DateTime date)
    {
        settings.LastAppliedDate = date.Date;
        _configService.SaveSettings(settings);
    }
}
