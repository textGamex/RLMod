using ParadoxPower.CSharpExtensions;
using ParadoxPower.Process;
using RLMod.Core.Extensions;
using RLMod.Core.Helpers;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;
using ZLinq;

namespace RLMod.Core.Services;

public sealed class ProvinceService(AppSettingService settingService)
{
    public IReadOnlyCollection<IEnumerable<int>> GetOceanProvinces(IReadOnlyDictionary<int, Province> provinces)
    {
        string path = Path.Combine(settingService.GameRootFolderPath, "map", "strategicregions");

        var oceanStates = new List<IEnumerable<int>>(64);
        foreach (var rootNode in ParseHelper.ParseAllFileToNodes(path, ParseFileType.Text))
        {
            foreach (
                var strategicRegionNode in rootNode
                    .Nodes.AsValueEnumerable()
                    .Where(node => node.Key.EqualsIgnoreCase("strategic_region"))
            )
            {
                if (!IsOcean(strategicRegionNode, provinces))
                {
                    continue;
                }

                if (!strategicRegionNode.TryGetNode("provinces", out var provincesNode))
                {
                    continue;
                }

                var provinceList = new List<int>(8);
                foreach (var provinceIdValue in provincesNode.LeafValues)
                {
                    if (int.TryParse(provinceIdValue.ValueText, out int provinceId))
                    {
                        provinceList.Add(provinceId);
                    }
                }
                oceanStates.Add(provinceList);
            }
        }

        return oceanStates;
    }

    private static bool IsOcean(Node strategicRegionNode, IReadOnlyDictionary<int, Province> provinces)
    {
        if (strategicRegionNode.TryGetLeaf("naval_terrain", out _))
        {
            return true;
        }

        if (!strategicRegionNode.TryGetNode("provinces", out var provinceNode))
        {
            return false;
        }

        if (!int.TryParse(provinceNode.LeafValues.FirstOrDefault()?.ValueText, out int provinceId))
        {
            return false;
        }

        if (provinces.TryGetValue(provinceId, out var province) && province.Type == ProvinceType.Sea)
        {
            return true;
        }

        return false;
    }
}
