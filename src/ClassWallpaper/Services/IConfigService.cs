using ClassWallpaper.Models;

namespace ClassWallpaper.Services;

/// <summary>
/// 配置系统服务：统一管理 D:\ClassWallpaper\Config 下的三个 JSON 配置文件
/// （settings.json / users.json / schedule.json），支持读取、保存与异常自愈。
/// </summary>
public interface IConfigService
{
    /// <summary>加载应用设置（settings.json）；兼容 Phase 1 调用，等价于 GetSettings()。</summary>
    AppConfig Load();

    /// <summary>获取应用设置（首次访问时自动创建/自愈）。</summary>
    AppConfig GetSettings();

    /// <summary>获取班级用户列表。</summary>
    UsersConfig GetUsers();

    /// <summary>获取壁纸切换计划。</summary>
    ScheduleConfig GetSchedule();

    /// <summary>保存应用设置（写盘失败时抛出异常）。</summary>
    void SaveSettings(AppConfig config);

    /// <summary>保存班级用户列表。</summary>
    void SaveUsers(UsersConfig config);

    /// <summary>保存壁纸切换计划。</summary>
    void SaveSchedule(ScheduleConfig config);
}
