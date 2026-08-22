using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hyakkei.Core;

/// <summary>应用级配置。每个工具的配置放在 Tools 节内，key = ITool.Id。</summary>
public sealed class AppConfig
{
    /// <summary>双击 Ctrl 判定窗口（第一次抬起到第二次按下的最大间隔，毫秒）。</summary>
    public int DoubleCtrlIntervalMs { get; set; } = 400;

    /// <summary>模块通用热键：仅在某个模块激活（岛内进入其面板）期间注册到系统。</summary>
    public string ModuleHotkey { get; set; } = "F6";

    /// <summary>System | Light | Dark（仅作用于管理中心；岛恒为深色）。</summary>
    public string Theme { get; set; } = "System";

    /// <summary>首次运行已完成（用于决定是否自动打开管理中心）。</summary>
    public bool FirstRunDone { get; set; }

    /// <summary>被停用的模块 Id 列表；不在列表中即启用。</summary>
    public List<string> DisabledTools { get; set; } = [];

    public JsonObject Tools { get; set; } = [];
}

/// <summary>JSON 配置读写，存放于程序目录 config/settings.json（绿色便携）。</summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public AppConfig Current { get; private set; } = new();

    public ConfigService()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "config");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), Options) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            Log.Error("读取配置失败，使用默认配置", ex);
            Current = new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
        }
        catch (Exception ex)
        {
            Log.Error("保存配置失败", ex);
        }
    }

    public T GetToolConfig<T>(string toolId) where T : new()
        => Current.Tools.TryGetPropertyValue(toolId, out var node) && node is not null
            ? node.Deserialize<T>(Options) ?? new T()
            : new T();

    public void SetToolConfig<T>(string toolId, T value)
    {
        Current.Tools[toolId] = JsonSerializer.SerializeToNode(value, Options);
        Save();
    }
}
