using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MathNet.Numerics.Random;
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

    [ObservableProperty]
    private int _generateCountryCount = settingService.GenerateCountryCount;

    [ObservableProperty]
    private int _randomSeed = settingService.RandomSeed ?? 0;

    [ObservableProperty]
    private bool _isInputRandomSeed;

    [ObservableProperty]
    private bool _isGenerateFileMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isGenerating;

    public bool IsIdle => !IsGenerating;

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
    private async Task GenerateRandomizerMap()
    {
        if (GenerateCountryCount <= 0)
        {
            MessageBox.Show("国家数量不能小于等于0", "错误");
            return;
        }

        if (IsInputRandomSeed)
        {
            settingService.RandomSeed = RandomSeed;
        }
        else
        {
            settingService.RandomSeed = Random.Shared.NextFullRangeInt32();
            RandomSeed = settingService.RandomSeed.Value;
        }

        IsGenerating = true;

        await Task.Run(() =>
        {
            string stateFolder = Path.Combine(GameRootPath, "history", "states");
            var states = GetStates(stateFolder);

            var generator = new MapGenerator(states, GenerateCountryCount);
            var countries = generator.GenerateRandomCountries().ToArray();

            // Log.Info("State Sum:{Sum}", countries.Sum(country => country.States.Count));
            double[] values = countries.Select(c => c.GetValue()).ToArray();
            double sum = values.AsValueEnumerable().Sum();
            double average = sum / countries.Length;
            double max = values.Max();
            double min = values.Min();
            Log.Info("国家总价值: {Sum}, 平均价值: {Average}", sum, average);
            Log.Info("最大值: {Max}, 最小值: {Min}", max, min);
            Log.Info("大于等于平均值的国家数量: {Count}", values.AsValueEnumerable().Count(v => v >= average));
            Log.Info("低于平均值的国家数量: {Count}", values.AsValueEnumerable().Count(v => v < average));
            Log.Info("最大国家States数量: {C}", countries.MaxBy(c => c.States.Count)!.States.Count);
            Log.Info("最小国家States数量: {C}", countries.MinBy(c => c.States.Count)!.States.Count);
            Log.Info("前六名国家发展度: {Array}", values.OrderDescending().Take(6));

            if (IsGenerateFileMode)
            {
                GenerateMod(countries);
            }
        });

        IsGenerating = false;
    }

    private void GenerateMod(IEnumerable<CountryInfo> countries)
    {
        string modPath = Path.Combine(settingService.OutputFolderPath, App.ModName);
        if (!Directory.Exists(modPath))
        {
            Directory.CreateDirectory(modPath);
        }
        string historyPath = Path.Combine(modPath, "history", "states");
        if (!Directory.Exists(historyPath))
        {
            Directory.CreateDirectory(historyPath);
        }

        CreateModDescriptionFile(modPath);

        foreach (var country in countries)
        {
            country.WriteToFiles();
        }
    }

    private void CreateModDescriptionFile(string modFolderPath)
    {
        string modDescriptionFilePath = Path.Combine(settingService.OutputFolderPath, $"{App.ModName}.mod");
        string modFilePath = Path.Combine(modFolderPath, "descriptor.mod");
        Child[] children =
        [
            ChildHelper.LeafQString("name", App.ModName),
            ChildHelper.LeafQString("path", modFolderPath.Replace('\\', '/')),
            ChildHelper.LeafQString("version", "0.1.0-beta"),
            ChildHelper.LeafQString("supported_version", "1.16.*"),
            ChildHelper.LeafQString("replace_path", "history/states")
        ];
        string content = CKPrinter.PrettyPrintStatements(
            children.Select(child => child.GetRawStatement("mod"))
        );
        File.WriteAllText(modDescriptionFilePath, content);
        File.WriteAllText(modFilePath, content);
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

    partial void OnGenerateCountryCountChanged(int value)
    {
        if (value > 0)
        {
            settingService.GenerateCountryCount = value;
        }
    }
}
