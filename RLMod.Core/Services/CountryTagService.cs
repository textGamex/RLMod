using MethodTimer;
using RLMod.Core.Helpers;

namespace RLMod.Core.Services;

public sealed class CountryTagService
{
    private readonly Lazy<string[]> _countryTags;
    private readonly AppSettingService _settingService;

    public CountryTagService(AppSettingService settingService)
    {
        _settingService = settingService;
        _countryTags = new Lazy<string[]>(ParseCountryTags);
    }

    public string[] GetCountryTags()
    {
        return _countryTags.Value;
    }

    [Time]
    private string[] ParseCountryTags()
    {
        string folderPath = Path.Combine(_settingService.GameRootFolderPath, Keywords.Common, "country_tags");

        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var countryTags = new HashSet<string>(256);
        foreach (var rootNode in ParseHelper.ParseAllFileToNodes(folderPath, ParseFileType.Text))
        {
            var leaves = rootNode.Leaves.ToArray();
            // 不加载临时标签
            if (
                Array.Exists(
                    leaves,
                    leaf =>
                        leaf.Key.Equals("dynamic_tags", StringComparison.OrdinalIgnoreCase)
                        && leaf.ValueText.Equals("yes", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }

            foreach (var leaf in leaves)
            {
                string countryTag = leaf.Key;
                // 国家标签长度必须为 3
                if (countryTag.Length != 3)
                {
                    continue;
                }
                countryTags.Add(countryTag);
            }
        }

        return countryTags.ToArray();
    }
}
