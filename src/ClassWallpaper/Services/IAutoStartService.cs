namespace ClassWallpaper.Services;

/// <summary>
/// 开机自启服务：通过 HKCU\...\Run 注册表键控制当前用户登录自启
/// （无需管理员权限）。部署电脑解冻状态下开启即可持久保存。
/// </summary>
public interface IAutoStartService
{
    /// <summary>当前是否已启用开机自启。</summary>
    bool IsEnabled();

    /// <summary>开启开机自启（写入 Run 键；失败抛出异常）。</summary>
    void Enable();

    /// <summary>关闭开机自启（删除 Run 键；失败抛出异常）。</summary>
    void Disable();
}
