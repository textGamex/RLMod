using System.Text.Json;
using RLMod.Core.Models.Settings;

namespace RLMod.Core.Services;

//TODO: 先从游戏建筑中动态生成一个配置信息, 待玩家调整后再使用
public sealed class BuildingGenerateSettingService
{
    public IEnumerable<BuildingGenerateSetting> BuildingGenerateSettings => _settings;
    private readonly BuildingGenerateSetting[] _settings;

    public BuildingGenerateSettingService()
    {
        string path = Path.Combine(App.Assets, "BuildingGenerateConfig.json");
        if (!File.Exists(path))
        {
            _settings = [];
            return;
        }

        _settings = JsonSerializer.Deserialize<BuildingGenerateSetting[]>(File.ReadAllText(path)) ?? [];
    }
}
