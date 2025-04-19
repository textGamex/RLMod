using ParadoxPower.CSharpExtensions;
using ParadoxPower.Process;
using RLMod.Core.Extensions;
using RLMod.Core.Helpers;
using RLMod.Core.Models.Map;
using ZLinq;

namespace RLMod.Core.Services;

public sealed class StateCategoryService
{
    public IEnumerable<StateCategory> StateCategories => _stateCategories;
    private readonly StateCategory[] _stateCategories;

    public StateCategoryService(AppSettingService settingService)
    {
        string path = Path.Combine(settingService.GameRootFolderPath, Keywords.Common, "state_category");

        var stateCategories = new List<StateCategory>(12);
        foreach (var rootNode in ParseHelper.ParseAllFileToNodes(path, ParseFileType.Text))
        {
            foreach (
                var stateCategoriesNode in rootNode
                    .Nodes.AsValueEnumerable()
                    .Where(node => node.Key.EqualsIgnoreCase("state_categories"))
            )
            {
                ParseStateCategoriesNodeToList(stateCategoriesNode, stateCategories);
            }
        }

        _stateCategories = stateCategories.ToArray();
    }

    private static void ParseStateCategoriesNodeToList(
        Node stateCategoriesNode,
        List<StateCategory> stateCategories
    )
    {
        foreach (var stateCategoryNode in stateCategoriesNode.Nodes)
        {
            int slots = 0;

            if (
                stateCategoryNode.TryGetLeaf("local_building_slots", out var leaf)
                && int.TryParse(leaf.ValueText, out int slotsValue)
            )
            {
                slots = slotsValue;
            }
            stateCategories.Add(new StateCategory(stateCategoryNode.Key, slots));
        }
    }
}
