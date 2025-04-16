using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Process;
using RLMod.Core.Extensions;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;
using RLMod.Core.Services;
using ZLinq;

namespace RLMod.Core;

public sealed partial class MainWindowViewModel(AppSettingService settingService) : ObservableObject
{
    [ObservableProperty]
    private string _gameRootPath = settingService.GameRootFolderPath;

    [RelayCommand]
    private void SelectGameRootPath()
    {
        var dialog = new OpenFolderDialog { Multiselect = false, Title = "Select Game Root Path" };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        GameRootPath = dialog.FolderName;
        settingService.GameRootFolderPath = GameRootPath;
    }

    [RelayCommand]
    private void GenerateRandomizerMap()
    {
        string stateFolder = Path.Combine(GameRootPath, "history", "states");
        var states = GetStates(stateFolder);
    }

    private List<State> GetStates(string stateFolder)
    {
        var states = new List<State>(1024);
        foreach (string path in Directory.EnumerateFiles(stateFolder))
        {
            if (!TextParser.TryParse(path, out var rootNode, out _))
            {
                continue;
            }

            foreach (
                var stateNode in rootNode
                    .Nodes.AsValueEnumerable()
                    .Where(node => node.Key.EqualsIgnoreCase("state"))
            )
            {
                states.AddRange(GetStateFormNode(stateNode));
            }
        }

        return states;
    }

    private static List<State> GetStateFormNode(Node stateNode)
    {
        // 一般来说, 一个文件中只会有一个 state
        var states = new List<State>(1);
        foreach (var stateChild in stateNode.AllArray)
        {
            var state = new State();
            if (stateChild.TryGetLeaf(out var leaf))
            {
                ParseLeaf(leaf, state);
            }
            else if (stateChild.TryGetNode(out var node))
            {
                if (node.Key.EqualsIgnoreCase("provinces"))
                {
                    state.Provinces = ParseProvinces(node);
                }
                else if (node.Key.EqualsIgnoreCase("history"))
                {
                    state.VictoryPoints = ParseVictoryPointsFormHistoryNode(node);
                }
            }
            states.Add(state);
        }

        return states;
    }

    private static void ParseLeaf(Leaf leaf, State state)
    {
        if (leaf.Key.EqualsIgnoreCase("id") && ushort.TryParse(leaf.ValueText, out ushort idValue))
        {
            state.Id = idValue;
        }
        else if (leaf.Key.EqualsIgnoreCase("name"))
        {
            state.Name = leaf.ValueText;
        }
        else if (leaf.Key.EqualsIgnoreCase("manpower") && int.TryParse(leaf.ValueText, out int manpowerValue))
        {
            state.Manpower = manpowerValue;
        }
        else if (leaf.Key.EqualsIgnoreCase("state_category"))
        {
            state.Category = leaf.ValueText;
        }
    }

    private static int[] ParseProvinces(Node node)
    {
        var provincesList = new List<int>(4);
        foreach (var leafValue in node.LeafValues)
        {
            if (int.TryParse(leafValue.ValueText, out int province))
            {
                provincesList.Add(province);
            }
        }

        return provincesList.ToArray();
    }

    private static VictoryPoint[] ParseVictoryPointsFormHistoryNode(Node historyNode)
    {
        var victoryPointList = new List<VictoryPoint>(2);

        foreach (
            var victoryPointsNode in historyNode
                .Nodes.AsValueEnumerable()
                .Where(n => n.Key.EqualsIgnoreCase("victory_points"))
        )
        {
            var array = victoryPointsNode.LeafValues.ToArray();
            if (array.Length != 2)
            {
                continue;
            }

            if (
                int.TryParse(array[0].ValueText, out int provinceId)
                && int.TryParse(array[1].ValueText, out int pointValue)
            )
            {
                victoryPointList.Add(new VictoryPoint(provinceId, pointValue));
            }
        }

        return victoryPointList.ToArray();
    }
}
