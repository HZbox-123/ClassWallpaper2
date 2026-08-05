using System.IO;
using System.Text;
using ClassWallpaper.Utils;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 日志服务：基于 Serilog，日志写入 D:\ClassWallpaper\Logs\ClassWallpaper-YYYYMMDD.log，
/// 按天滚动，保留天数由配置决定（默认 30 天）。
/// </summary>
public static class LogService
{
    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>初始化全局日志器；需在配置加载前调用（日志目录固定 D 盘）。</summary>
    public static void Initialize(int retentionDays = 30)
    {
        PathHelper.EnsureDirectories();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(PathHelper.LogsDir, "ClassWallpaper-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retentionDays,
                outputTemplate: OutputTemplate,
                encoding: Encoding.UTF8)
            .CreateLogger();
    }

    /// <summary>冲刷并关闭日志器（应用退出时调用）。</summary>
    public static void Shutdown() => Log.CloseAndFlush();
}
