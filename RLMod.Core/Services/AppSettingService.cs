using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;
using RLMod.Core.Models.Settings;

namespace RLMod.Core.Services;

public sealed class AppSettingService
{
    public string GameRootFolderPath
    {
        get => _gameRootFolderPath;
        set => SetProperty(ref _gameRootFolderPath, value);
    }
    private string _gameRootFolderPath = string.Empty;

    public string OutputFolderPath
    {
        get => _outputFolderPath;
        set => SetProperty(ref _outputFolderPath, value);
    }
    private string _outputFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Paradox Interactive",
        "Hearts of Iron IV",
        "mod"
    );

    public StateGenerateSettings StateGenerate { get; }

    public int GenerateCountryCount
    {
        get => _generateCountryCount;
        set => SetProperty(ref _generateCountryCount, value);
    }
    private int _generateCountryCount = 64;

    /// <summary>
    /// 全局随机数生成器的种子, 相同的种子应生成相同的 Mod
    /// </summary>
    [JsonIgnore]
    public int? RandomSeed { get; set; }

    [JsonIgnore]
    public bool IsChanged { get; set; }
    private bool AnyChanges => IsChanged || StateGenerate.IsChanged;

    private static readonly string ConfigFilePath = Path.Combine(App.AppConfigPath, "AppSettings.json");

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private AppSettingService()
    {
        StateGenerate = new StateGenerateSettings();
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

    [JsonConstructor]
    public AppSettingService(string gameRootFolderPath, StateGenerateSettings stateGenerate)
    {
        _gameRootFolderPath = gameRootFolderPath;
        StateGenerate = stateGenerate;
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// 如果有更改，保存更改
    /// </summary>
    public void SaveChanged()
    {
        if (!AnyChanges)
        {
            Log.Info("配置文件未改变, 跳过写入");
            return;
        }

        Log.Info("配置文件保存中...");

        File.WriteAllBytes(ConfigFilePath, JsonSerializer.SerializeToUtf8Bytes(this, Options));
        IsChanged = false;
        StateGenerate.IsChanged = false;

        Log.Info("配置文件保存完成");
    }

    public static AppSettingService Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new AppSettingService();
        }

        var result =
            JsonSerializer.Deserialize<AppSettingService>(File.ReadAllBytes(ConfigFilePath))
            ?? new AppSettingService();

        return result;
    }
}
