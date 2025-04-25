using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NLog;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Parser;
using ParadoxPower.Process;
using RLMod.Core.Extensions;
using RLMod.Core.Helpers;
using RLMod.Core.Infrastructure.Generator;
using RLMod.Core.Models.Map;
using RLMod.Core.Services;
using ZLinq;

namespace RLMod.Core;

public sealed partial class MainWindowViewModel(AppSettingService settingService) : ObservableObject
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

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
        var generator = new MapGenerator(states);
        var countries = generator.GenerateRandomCountries();
        Log.Info("State Sum:{Sum}", countries.Sum(country => country.States.Count));
    }

    private void GenerateMod(IEnumerable<CountryInfo> countries)
    {
        string modPath = Path.Combine(settingService.OutputFolderPath, App.ModName);
        if (!Directory.Exists(modPath))
        {
            Directory.CreateDirectory(modPath);
        }
        string modDescriptionFilePath = Path.Combine(settingService.OutputFolderPath, $"{App.ModName}.mod");

        Child[] children =
        [
            ChildHelper.LeafQString("name", App.ModName),
            ChildHelper.LeafQString("path", modPath),
            ChildHelper.LeafQString("version", "0.1.0-beta"),
            ChildHelper.LeafQString("supported_version", "1.16.*")
        ];
        File.WriteAllText(
            modDescriptionFilePath,
            CKPrinter.PrettyPrintStatements(children.Select(child => child.GetRawStatement("mod")))
        );

        foreach (var country in countries)
        {
            country.WriteToFiles();
        }
    }

    private List<State> GetStates(string stateFolder)
    {
        var states = new List<State>(1024);
        foreach (var rootNode in ParseHelper.ParseAllFileToNodes(stateFolder, ParseFileType.Text))
        {
            // 一般来说, 一个文件中只有一个 state 节点, 但以防万一
            foreach (
                var stateNode in rootNode
                    .Nodes.AsValueEnumerable()
                    .Where(node => node.Key.EqualsIgnoreCase("state"))
            )
            {
                states.Add(GetStateFormNode(stateNode));
            }
        }

        return states;
    }

    private static State GetStateFormNode(Node stateNode)
    {
        var state = new State();
        foreach (var stateChild in stateNode.AllArray)
        {
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
        }

        return state;
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
        else if (leaf.Key.EqualsIgnoreCase("impassable") && leaf.Value.TryGetBool(out bool isImpassable))
        {
            state.IsImpassable = isImpassable;
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
