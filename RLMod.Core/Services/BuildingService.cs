using System.Collections.Frozen;
using NLog;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Process;
using RLMod.Core.Extensions;
using RLMod.Core.Helpers;
using RLMod.Core.Models;

namespace RLMod.Core.Services;

public sealed class BuildingService
{
    private readonly FrozenDictionary<string, BuildingInfo> _buildings;
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string BuildingsKeyword = "buildings";

    public BuildingService(AppSettingService appSettingService)
    {
        string path = Path.Combine(appSettingService.GameRootFolderPath, Keywords.Common, BuildingsKeyword);

        var buildingMap = new Dictionary<string, BuildingInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootNode in ParseHelper.ParseAllFileToNodes(path, ParseFileType.Text))
        {
            if (!rootNode.TryGetNode(BuildingsKeyword, out var buildingsNode))
            {
                Log.Warn("buildings node not found");
                continue;
            }

            ParseBuildingNodeToDictionary(buildingsNode.Nodes, buildingMap);
        }

        _buildings = buildingMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static void ParseBuildingNodeToDictionary(
        IEnumerable<Node> buildingNodes,
        Dictionary<string, BuildingInfo> buildings
    )
    {
        foreach (var buildingNode in buildingNodes)
        {
            ParseBuildingNodeToDictionary(buildingNode, buildings);
        }
    }

    private static void ParseBuildingNodeToDictionary(
        Node buildingNode,
        Dictionary<string, BuildingInfo> buildings
    )
    {
        int maxLevel = 0;
        var levelCapNode = buildingNode.Nodes.FirstOrDefault(node =>
            StringComparer.OrdinalIgnoreCase.Equals(node.Key, "level_cap")
        );

        if (levelCapNode is null)
        {
            Log.Warn("建筑 {Building} 的 level_cap node 未找到, 不进行处理", buildingNode.Key);
            return;
        }

        foreach (var levelPropertyLeaf in levelCapNode.Leaves)
        {
            if (
                levelPropertyLeaf.Key.EqualsIgnoreCase("state_max")
                || levelPropertyLeaf.Key.EqualsIgnoreCase("province_max")
            )
            {
                if (levelPropertyLeaf.Value.TryGetInt(out int value))
                {
                    maxLevel = value;
                }
                break;
            }
        }

        buildings[buildingNode.Key] = new BuildingInfo(buildingNode.Key, maxLevel);
    }

    public BuildingInfo this[string key] => _buildings[key];
}
