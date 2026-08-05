namespace ClassWallpaper.Models;

/// <summary>班级用户列表（users.json）。</summary>
public sealed class UsersConfig
{
    /// <summary>配置结构版本号。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>班级用户列表。</summary>
    public List<UserConfig> Users { get; set; } = new();
}
