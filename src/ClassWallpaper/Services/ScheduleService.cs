using System;
using System.Collections.Generic;
using System.Linq;
using ClassWallpaper.Models;
using ClosedXML.Excel;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 排班服务实现：
/// - 日期规则：起止日期内匹配自定义星期几（可多选），节假日自动跳过；
/// - 人员规则：按 users.json 排序顺序循环分配，同一周内每人最多一次；
/// - 区间化：每个排班日生成一个连续区间（开始=排班日，结束=下一个排班日前一天），
///   中间非排班日期归属于当前区间，保证每天都有覆盖；
/// - 生成参数与结果一并保存到 schedule.json，界面可回显并手动修改。
/// </summary>
public sealed class ScheduleService : IScheduleService
{
    private readonly IConfigService _configService;

    public ScheduleService(IConfigService configService)
    {
        _configService = configService;
    }

    public ScheduleConfig GetSchedule()
    {
        var schedule = _configService.GetSchedule();
        schedule.Items.Sort((a, b) => a.Date.CompareTo(b.Date));
        return schedule;
    }

    public void Generate(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<DayOfWeek> weekDays,
        IReadOnlyCollection<DateTime> holidays)
    {
        if (weekDays.Count == 0)
        {
            throw new ArgumentException("至少选择一个星期几", nameof(weekDays));
        }

        if (endDate.Date < startDate.Date)
        {
            throw new ArgumentException("结束日期不能早于开始日期", nameof(endDate));
        }

        var users = _configService.GetUsers().Users.OrderBy(u => u.Order).Select(u => u.Name).ToList();
        if (users.Count == 0)
        {
            throw new InvalidOperationException("人员列表为空，请先在「人员管理」中添加人员");
        }

        // 1) 候选排班日：起止范围内匹配星期、排除节假日
        var holidaySet = holidays.Select(h => h.Date).ToHashSet();
        var weekDaySet = weekDays.ToHashSet();
        var dates = new List<DateTime>();
        for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
        {
            if (weekDaySet.Contains(day.DayOfWeek) && !holidaySet.Contains(day))
            {
                dates.Add(day);
            }
        }

        // 2) 分配：顺序循环 + 同一周内每人最多一次（周一为周界）
        var items = new List<ScheduleItem>(dates.Count);
        var weekUsed = new HashSet<string>();
        var currentWeekStart = DateTime.MinValue;
        var cursor = 0;

        foreach (var date in dates)
        {
            var weekStart = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
            if (weekStart != currentWeekStart)
            {
                weekUsed.Clear();
                currentWeekStart = weekStart;
            }

            var found = false;
            for (var i = 0; i < users.Count; i++)
            {
                var candidate = users[(cursor + i) % users.Count];
                if (!weekUsed.Contains(candidate))
                {
                    weekUsed.Add(candidate);
                    cursor = (cursor + i + 1) % users.Count;
                    items.Add(new ScheduleItem { Date = date, Name = candidate });
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // 本周所有人都已轮值但仍有余日（排班日多于人数）→ 放宽限制
                var name = users[cursor % users.Count];
                cursor = (cursor + 1) % users.Count;
                items.Add(new ScheduleItem { Date = date, Name = name });
            }
        }

        // 3) 区间化：每个排班日负责到下一个排班日前一天（最后一段到计划结束日期），
        //    中间的非排班日期归属于当前区间，保证每天都有覆盖
        for (var i = 0; i < items.Count; i++)
        {
            items[i].EndDate = i < items.Count - 1
                ? items[i + 1].Date.AddDays(-1)
                : endDate.Date;
        }

        // 4) 参数与结果一并保存
        var config = new ScheduleConfig
        {
            Items = items,
            WeekDays = weekDays.Select(w => (int)w).OrderBy(v => v).ToList(),
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            Holidays = holidaySet.OrderBy(h => h).ToList(),
        };
        _configService.SaveSchedule(config);
        Log.Information(
            "排班生成完成：{Count} 个区间（{Start} ~ {End}，星期 {Days}，节假日 {HolidayCount}），人员 {People} 人",
            items.Count, startDate.Date, endDate.Date,
            string.Join(",", weekDaySet.Select(w => (int)w).OrderBy(v => v)),
            holidaySet.Count, users.Count);
    }

    public void Save(ScheduleConfig schedule)
    {
        schedule.Items.Sort((a, b) => a.Date.CompareTo(b.Date));
        _configService.SaveSchedule(schedule);
        Log.Information("排班计划已保存：{Count} 个区间", schedule.Items.Count);
    }

    /// <summary>
    /// 导出排班计划为 Excel：表头「开始日期 | 结束日期 | 星期几 | 人员姓名」，
    /// 数据与界面一致（区间一行）；已排序列宽、表头样式、日期格式与细边框。
    /// </summary>
    public void ExportToExcel(string filePath)
    {
        var items = GetSchedule().Items;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("排班计划");

        sheet.Cell(1, 1).Value = "开始日期";
        sheet.Cell(1, 2).Value = "结束日期";
        sheet.Cell(1, 3).Value = "星期几";
        sheet.Cell(1, 4).Value = "人员姓名";
        var header = sheet.Range(1, 1, 1, 4);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#3B6FD4");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Date;
            sheet.Cell(row, 2).Value = item.EndDateOrDate;
            sheet.Cell(row, 3).Value = item.WeekdayText;
            sheet.Cell(row, 4).Value = item.Name;
            sheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
            sheet.Cell(row, 2).Style.DateFormat.Format = "yyyy-MM-dd";
            row++;
        }

        sheet.Column(1).Width = 16;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 10;
        sheet.Column(4).Width = 20;
        var body = sheet.Range(1, 1, row - 1, 4);
        body.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        body.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        workbook.SaveAs(filePath);
        Log.Information("排班计划已导出 Excel：{Path}（{Count} 条）", filePath, items.Count);
    }
}
