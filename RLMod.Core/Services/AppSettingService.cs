using MemoryPack;
using NLog;
using RLMod.Core.Models.Settings;

namespace RLMod.Core.Services;

[MemoryPackable]
public sealed partial class AppSettingService
{
    [MemoryPackOrder(0)]
    public string GameRootFolderPath
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    [MemoryPackOrder(1)]
    public StateGenerateSettings StateGenerate { get; set; }

    /// <summary>
    /// 全局随机数生成器的种子, 相同的种子应生成相同的 Mod
    /// </summary>
    [MemoryPackIgnore]
    public int? RandomSeed { get; set; }

    [MemoryPackIgnore]
    public bool IsChanged { get; set; }

    private static readonly string ConfigFilePath = Path.Combine(App.AppConfigPath, "AppSettings.bin");

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private AppSettingService()
    {
        StateGenerate = new StateGenerateSettings { SettingService = this };
    }

    private void SetProperty<T>(ref T field, T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return;
        }
        field = newValue;
        IsChanged = true;
    }

    /// <summary>
    /// 如果有更改，保存更改
    /// </summary>
    public void SaveChanged()
    {
        if (!IsChanged)
        {
            Log.Info("配置文件未改变, 跳过写入");
            return;
        }

        Log.Info("配置文件保存中...");
        // TODO: System.IO.Pipelines
        File.WriteAllBytes(ConfigFilePath, MemoryPackSerializer.Serialize(this));
        IsChanged = false;
        Log.Info("配置文件保存完成");
    }

    public static AppSettingService Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new AppSettingService();
        }

        var result = MemoryPackSerializer.Deserialize<AppSettingService>(File.ReadAllBytes(ConfigFilePath));

        if (result is null)
        {
            result = new AppSettingService();
        }
        else
        {
            result.IsChanged = false;
        }

        return result;
    }
}
