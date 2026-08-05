using ClassWallpaper.Models;

namespace ClassWallpaper.Services;

/// <summary>
/// 人员管理服务：班级用户 CRUD 与 Excel 导入（保存 users.json）。
/// </summary>
public interface IUserService
{
    /// <summary>获取全部用户（按 Order 排序）。</summary>
    List<UserConfig> GetUsers();

    /// <summary>添加用户；name 为空返回 false。order 为空时追加到末尾。</summary>
    bool AddUser(string name, int? order = null);

    /// <summary>删除用户。</summary>
    void RemoveUser(UserConfig user);

    /// <summary>上移/下移（delta = -1 上移，+1 下移）。</summary>
    void MoveUser(UserConfig user, int delta);

    /// <summary>从 Excel（.xlsx）导入姓名与排序，返回导入条数。</summary>
    int ImportFromExcel(string filePath);
}
