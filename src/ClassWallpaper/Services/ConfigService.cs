using System.IO;
using System.Text.Json;
using ClassWallpaper.Models;
using ClassWallpaper.Utils;
using Serilog;

namespace ClassWallpaper.Services;

/// <summary>
/// 配置系统实现：
/// - 三个配置文件（settings.json / users.json / schedule.json）统一读写；
/// - 文件不存在时自动创建默认配置（与项目 Config 目录模板一致）；
/// - 文件损坏（JSON 解析失败）时备份为 *.bad-时间戳 后重建默认值；
/// - 读取带内存缓存，保存立即写盘并更新缓存；
/// - 写盘失败抛出异常，由调用方决定处理方式（启动阶段由 App 统一兜底）。
/// </summary>
public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly object _lock = new();
    private readonly string _settingsPath;
    private readonly string _usersPath;
    private readonly string _schedulePath;

    private AppConfig? _settings;
    private UsersConfig? _users;
    private ScheduleConfig? _schedule;

    public ConfigService()
        : this(PathHelper.ConfigFilePath, PathHelper.UsersFilePath, PathHelper.ScheduleFilePath)
    {
    }

    /// <summary>便于测试：可指定三个配置文件路径。</summary>
    public ConfigService(string settingsPath, string usersPath, string schedulePath)
    {
        _settingsPath = settingsPath;
        _usersPath = usersPath;
        _schedulePath = schedulePath;
    }

    /// <summary>兼容 Phase 1 调用。</summary>
    public AppConfig Load() => GetSettings();

    public AppConfig GetSettings()
    {
        if (_settings is not null)
        {
            return _settings;
        }

        lock (_lock)
        {
            return _settings ??= File.Exists(_settingsPath)
                ? LoadFromDisk(_settingsPath, () => new AppConfig())
                : MigrateOrCreateSettings();
        }
    }

    public UsersConfig GetUsers() => GetOrLoad(ref _users, _usersPath, () => new UsersConfig());

    public ScheduleConfig GetSchedule() => GetOrLoad(ref _schedule, _schedulePath, () => new ScheduleConfig());

    public void SaveSettings(AppConfig config) => Save(ref _settings, _settingsPath, config);

    public void SaveUsers(UsersConfig config) => Save(ref _users, _usersPath, config);

    public void SaveSchedule(ScheduleConfig config) => Save(ref _schedule, _schedulePath, config);

    // ---------- 内部实现 ----------

    private T GetOrLoad<T>(ref T? cache, string path, Func<T> createDefault)
        where T : class, new()
    {
        if (cache is not null)
        {
            return cache;
        }

        lock (_lock)
        {
            return cache ??= LoadFromDisk(path, createDefault);
        }
    }

    private void Save<T>(ref T? cache, string path, T config)
        where T : class
    {
        lock (_lock)
        {
            SaveToDisk(path, config);
            cache = config;
        }
    }

    /// <summary>
    /// 读盘并反序列化；文件不存在或损坏时备份（仅损坏时）并重建默认值写盘。
    /// </summary>
    private static T LoadFromDisk<T>(string path, Func<T> createDefault)
        where T : class, new()
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var obj = JsonSerializer.Deserialize<T>(json, JsonOptions);
                if (obj is not null)
                {
                    Log.Information("已加载配置文件：{Path}", path);
                    return obj;
                }

                Log.Warning("配置文件内容无效，将重建默认值：{Path}", path);
            }
            else
            {
                Log.Information("配置文件不存在，创建默认配置：{Path}", path);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取配置文件失败，将备份并重建默认值：{Path}", path);
            BackupCorruptFile(path);
        }

        var defaults = createDefault();
        SaveToDisk(path, defaults);
        return defaults;
    }

    /// <summary>
    /// settings.json 不存在时，若存在 Phase 1 遗留的 appsettings.json 则迁移其内容，否则创建默认值。
    /// </summary>
    private AppConfig MigrateOrCreateSettings()
    {
        var legacyPath = Path.Combine(PathHelper.ConfigDir, "appsettings.json");
        if (File.Exists(legacyPath))
        {
            try
            {
                var legacy = JsonSerializer.Deserialize<LegacyAppSettings>(File.ReadAllText(legacyPath), JsonOptions);
                if (legacy is not null)
                {
                    var migrated = new AppConfig
                    {
                        SchemaVersion = legacy.SchemaVersion,
                        LogRetentionDays = legacy.LogRetentionDays,
                    };
                    SaveToDisk(_settingsPath, migrated);
                    File.Delete(legacyPath);
                    Log.Information("已迁移旧配置 appsettings.json → settings.json");
                    return migrated;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "旧配置 appsettings.json 解析失败，跳过迁移，使用默认配置");
            }
        }

        Log.Information("配置文件不存在，创建默认配置：{Path}", _settingsPath);
        var defaults = new AppConfig();
        SaveToDisk(_settingsPath, defaults);
        return defaults;
    }

    private static void SaveToDisk<T>(string path, T config)
    {
        PathHelper.EnsureDirectories();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
        Log.Information("已保存配置文件：{Path}", path);
    }

    /// <summary>损坏文件备份为 {原路径}.bad-{时间戳}，避免覆盖丢失用户数据。</summary>
    private static void BackupCorruptFile(string path)
    {
        try
        {
            var backupPath = $"{path}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(path, backupPath);
            Log.Warning("已备份损坏配置：{BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "备份损坏配置失败：{Path}", path);
        }
    }

    /// <summary>Phase 1 遗留配置结构（appsettings.json）。</summary>
    private sealed class LegacyAppSettings
    {
        public int SchemaVersion { get; set; } = 1;

        public int LogRetentionDays { get; set; } = 30;
    }
}
