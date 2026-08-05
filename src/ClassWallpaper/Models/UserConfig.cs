namespace ClassWallpaper.Models;

/// <summary>班级用户（users.json 中的一条）。</summary>
public sealed class UserConfig
{
    /// <summary>学生姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>排序号（1..N，由 UserService 维护，Excel 导入可指定位置）。</summary>
    public int Order { get; set; }
}
