using System.Diagnostics;
using System.Text.Json;
using RLMod.Core.Models.Settings;

namespace RLMod.Core.Services;

//TODO: 集中管理?
public sealed class ResourceGenerateSettingService
{
    public IEnumerable<ResourceGenerateSetting> Settings => _settings;
    private readonly ResourceGenerateSetting[] _settings;

    public ResourceGenerateSettingService()
    {
        string path = Path.Combine(App.Assets, "ResourceGenerateConfig.json");
        if (!File.Exists(path))
        {
            _settings = [];
            return;
        }

        _settings = JsonSerializer.Deserialize<ResourceGenerateSetting[]>(File.ReadAllBytes(path)) ?? [];

        // 确保比例之和为 1
        Debug.Assert(Math.Abs(_settings.Sum(setting => setting.Proportion) - 1.0) < 0.0001);
    }
}
