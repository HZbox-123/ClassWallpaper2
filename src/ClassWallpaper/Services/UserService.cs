using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassWallpaper.Models;
using ClosedXML.Excel;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 人员管理服务实现：
/// - 基于 IConfigService 持久化到 users.json；
/// - 每次变更后按列表顺序重编号 Order（1..N），保证排序语义一致；
/// - Excel 导入支持表头识别（姓名/排序），排序列决定插入位置。
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IConfigService _configService;

    public UserService(IConfigService configService)
    {
        _configService = configService;
    }

    public List<UserConfig> GetUsers()
        => _configService.GetUsers().Users.OrderBy(u => u.Order).ToList();

    public bool AddUser(string name, int? order = null)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return false;
        }

        var users = _configService.GetUsers();
        var ordered = users.Users.OrderBy(u => u.Order).ToList();
        var position = order is null ? ordered.Count : Math.Clamp(order.Value - 1, 0, ordered.Count);
        ordered.Insert(position, new UserConfig { Name = name });
        RenumberAndSave(users, ordered);
        Log.Information("添加用户：{Name}（位置 {Position}）", name, position + 1);
        return true;
    }

    public void RemoveUser(UserConfig user)
    {
        var users = _configService.GetUsers();
        var ordered = users.Users.Where(u => !ReferenceEquals(u, user)).ToList();
        RenumberAndSave(users, ordered);
        Log.Information("删除用户：{Name}", user.Name);
    }

    public void MoveUser(UserConfig user, int delta)
    {
        var users = _configService.GetUsers();
        var ordered = users.Users.OrderBy(u => u.Order).ToList();
        var index = ordered.FindIndex(u => ReferenceEquals(u, user));
        var target = index + delta;
        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        RenumberAndSave(users, ordered);
        Log.Information("移动用户：{Name}（{Delta}）", user.Name, delta > 0 ? "下移" : "上移");
    }

    public int ImportFromExcel(string filePath)
    {
        var entries = ParseExcel(filePath);
        if (entries.Count == 0)
        {
            return 0;
        }

        var users = _configService.GetUsers();
        var ordered = users.Users.OrderBy(u => u.Order).ToList();

        // 按 Excel 排序列升序逐个插入到对应位置（排序号即目标位置）
        foreach (var (name, sort) in entries.OrderBy(e => e.Sort))
        {
            var position = Math.Clamp(sort - 1, 0, ordered.Count);
            ordered.Insert(position, new UserConfig { Name = name });
        }

        RenumberAndSave(users, ordered);
        Log.Information("Excel 导入 {Count} 人：{Path}", entries.Count, filePath);
        return entries.Count;
    }

    // ---------- 内部实现 ----------

    private void RenumberAndSave(UsersConfig users, List<UserConfig> ordered)
    {
        users.Users.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i + 1;
            users.Users.Add(ordered[i]);
        }

        _configService.SaveUsers(users);
    }

    /// <summary>
    /// 解析 Excel：第一行若为表头（含"姓名"/"名字"/"name"）则跳过；
    /// A 列为姓名，B 列（表头含"排序"/"序号"/"order"）为排序列，无排序列时按行序。
    /// </summary>
    private static List<(string Name, int Sort)> ParseExcel(string filePath)
    {
        var result = new List<(string, int)>();
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("Excel 文件中没有工作表");

        var rows = sheet.RowsUsed().ToList();
        if (rows.Count == 0)
        {
            return result;
        }

        var headerName = rows[0].Cell(1).GetString().Trim();
        var headerSort = rows[0].Cell(2).GetString().Trim();
        var hasHeader = headerName.Contains("姓名") || headerName.Contains("名字")
            || headerName.Contains("name", StringComparison.OrdinalIgnoreCase);
        var hasSortColumn = headerSort.Contains("排序") || headerSort.Contains("序号")
            || headerSort.Contains("order", StringComparison.OrdinalIgnoreCase);

        var rowIndex = 1;
        foreach (var row in hasHeader ? rows.Skip(1) : rows)
        {
            var name = row.Cell(1).GetString().Trim();
            if (name.Length == 0)
            {
                rowIndex++;
                continue;
            }

            var sort = 0;
            if (hasSortColumn && int.TryParse(row.Cell(2).GetString().Trim(), out var value))
            {
                sort = value;
            }

            result.Add((name, sort > 0 ? sort : rowIndex));
            rowIndex++;
        }

        return result;
    }
}
