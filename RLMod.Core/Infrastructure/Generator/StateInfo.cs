using System.Diagnostics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using Microsoft.Extensions.DependencyInjection;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Parser;
using ParadoxPower.Process;
using RLMod.Core.Helpers;
using RLMod.Core.Models.Map;
using RLMod.Core.Models.Settings;
using RLMod.Core.Services;
using ZLinq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfo : IEquatable<StateInfo>
{
    public int Id { get; }
    public CountryInfo? Owner { get; set; }
    public State State { get; }
    public int Factories { get; set; }
    public int Resources { get; set; }
    public IEnumerable<StateInfo> AdjacentStates => _adjacentStates;
    public StateType Type { get; }
    public StateCategory Category { get; }
    public bool IsImpassable { get; }
    public bool IsOcean { get; }
    public bool IsCoastal { get; }
    public bool IsPassableLand => !IsImpassable && !IsOcean;
    public int MaxFactories { get; }
    public int TotalVictoryPoint { get; }
    public StateBuildings Buildings { get; } = new();

    /// <summary>
    /// 计算获取省份的价值。
    /// </summary>
    /// <returns>省份的价值</returns>
    public double Value => IsImpassable || IsOcean ? 0 : GetValue();

    private double GetValue()
    {
        return (double)Factories
                / AppSettingService.StateGenerate.MaxFactoryNumber
                * 100
                * AppSettingService.StateGenerate.FactoryNumberWeight
            + (double)MaxFactories
                / AppSettingService.StateGenerate.MaxFactoryNumber
                * 100
                * AppSettingService.StateGenerate.MaxFactoryNumberWeight
            + (double)Resources
                / AppSettingService.StateGenerate.MaxResourceNumber
                * 100
                * AppSettingService.StateGenerate.ResourcesWeight
            + (double)TotalVictoryPoint
                / AppSettingService.StateGenerate.MaxVictoryPoint
                * 100
                * AppSettingService.StateGenerate.VictoryPointWeight;
    }

    private StateInfo[] _adjacentStates = [];
    private readonly MersenneTwister _random;

    private static int _oceanStateId = 0;

    private static readonly StateCategoryService StateCategoryService =
        App.Current.Services.GetRequiredService<StateCategoryService>();
    private static readonly AppSettingService AppSettingService =
        App.Current.Services.GetRequiredService<AppSettingService>();
    private static readonly BuildingGenerateSettingService BuildingGenerateSettingService =
        App.Current.Services.GetRequiredService<BuildingGenerateSettingService>();
    private static readonly BuildingService BuildingService =
        App.Current.Services.GetRequiredService<BuildingService>();

    public StateInfo(State state, bool isCoastal, StateType type)
    {
        Id = state.Id;
        _random = RandomHelper.GetRandomWithSeed();

        State = state;
        Type = type;
        IsCoastal = isCoastal;
        IsImpassable = state.IsImpassable;
        TotalVictoryPoint = state.VictoryPoints.Sum(point => point.Value);

        int maxFactoriesLimit = AppSettingService.StateGenerate.MaxFactoryNumber;

        int minFactories;
        int maxFactories;
        switch (Type)
        {
            case StateType.Industrial:
                minFactories = (int)(0.70 * maxFactoriesLimit);
                maxFactories = (int)(1.0 * maxFactoriesLimit);
                break;
            case StateType.Resource:
                minFactories = (int)(0.1 * maxFactoriesLimit);
                maxFactories = (int)(0.3 * maxFactoriesLimit);
                break;
            case StateType.Balanced:
            default:
                minFactories = (int)(0.3 * maxFactoriesLimit);
                maxFactories = (int)(0.7 * maxFactoriesLimit);
                break;
        }

        Category = GetRandomStateCategory(minFactories, maxFactories);
        MaxFactories = Category.Slots;

        int resourcesLimit = AppSettingService.StateGenerate.MaxResourceNumber;
        switch (type)
        {
            case StateType.Industrial:
                GenerateIndustrialProperties(MaxFactories, resourcesLimit);
                break;
            case StateType.Resource:
                GenerateResourceProperties(MaxFactories, resourcesLimit);
                break;
            case StateType.Balanced:
            default:
                GenerateBalancedProperties(MaxFactories, resourcesLimit);
                break;
        }
    }

    public StateInfo(int[] provinces)
    {
        State = new State { Provinces = provinces };
        Type = StateType.Balanced;
        Category = null!;
        _random = RandomHelper.GetRandomWithSeed();
        Id = --_oceanStateId;
        IsOcean = true;
    }

    public static void ResetOceanStateId()
    {
        _oceanStateId = 0;
    }

    public void SetAdjacent(StateInfo[] adjacent)
    {
        _adjacentStates = adjacent;
    }

    private StateCategory GetRandomStateCategory(int minSlots, int maxSlots)
    {
        var slots = new List<StateCategory>(8);
        foreach (var stateCategory in StateCategoryService.StateCategories)
        {
            if (stateCategory.Slots >= minSlots && stateCategory.Slots <= maxSlots)
            {
                slots.Add(stateCategory);
            }
        }

        return slots[_random.Next(0, slots.Count)];
    }

    private void GenerateIndustrialProperties(int maxFactories, int maxResources)
    {
        Factories = _random.Next((int)(maxFactories * 0.5), (int)(maxFactories * 0.7));

        int resourceMax = _random.Next(Factories * 10, (int)(maxResources * 0.3));
        Resources = _random.Next(0, resourceMax + 1);
    }

    private void GenerateResourceProperties(int maxFactories, int maxResources)
    {
        Resources = _random.Next((int)(maxResources * 0.7), maxResources + 1);

        int factoryMax = Math.Min((int)(Resources * 0.005), (int)(maxFactories * 0.7));
        Factories = _random.Next(0, factoryMax + 1);
    }

    private void GenerateBalancedProperties(int maxFactories, int maxResources)
    {
        Factories = _random.Next((int)(maxFactories * 0.3), (int)(maxFactories * 0.7) + 1);
        Resources = _random.Next((int)(maxResources * 0.3), (int)(maxResources * 0.7) + 1);

        double resourceStandard = Factories * 50.0;
        if (Math.Abs(Resources - resourceStandard) > 50)
        {
            Resources = (int)(resourceStandard + _random.Next(-25, 26));
            Resources = Math.Max(0, Math.Min(maxResources, Resources));
        }
    }

    public void GenerateBuildings()
    {
        // 确保比例之和为 1
        Debug.Assert(
            Math.Abs(
                BuildingGenerateSettingService.BuildingGenerateSettings.Sum(setting => setting.Proportion)
                    - 1.0
            ) < 0.0001
        );

        // 将替换生成类型放到最后处理
        foreach (
            var setting in BuildingGenerateSettingService
                .BuildingGenerateSettings.AsValueEnumerable()
                .OrderBy(building => building.Type == BuildingGenerateType.Replace)
        )
        {
            if (setting.NeedCoastal && !IsCoastal)
            {
                continue;
            }

            if (setting.Type == BuildingGenerateType.Necessary)
            {
                double level = Normal.Sample(_random, setting.Mean, setting.StandardDeviation);
                Buildings.Add(
                    setting.Name,
                    MathHelper.ClampValue(
                        (int)Math.Round(level),
                        setting.MinLevel ?? 1,
                        Math.Min(setting.MaxLevel ?? int.MaxValue, BuildingService[setting.Name].MaxLevel)
                    )
                );
            }
            else if (setting.Type == BuildingGenerateType.Replace)
            {
                Debug.Assert(setting.ReplaceSetting is not null);

                var replaceSetting = setting.ReplaceSetting;
                int replaceBuildingLevel = Buildings.GetLevel(replaceSetting.ReplaceName);
                int level = Math.Min(
                    (int)Math.Round(replaceBuildingLevel * replaceSetting.Proportion),
                    BuildingService[setting.Name].MaxLevel
                );
                if (level == 0)
                {
                    continue;
                }

                Buildings.Add(setting.Name, level);
                Buildings.SetLevel(replaceSetting.ReplaceName, replaceBuildingLevel - level);
            }
            else if (setting.Type == BuildingGenerateType.Proportion)
            {
                Buildings.Add(setting.Name, (int)Math.Round(Factories * setting.Proportion));
            }
        }
    }

    public string ToScript()
    {
        Debug.Assert(State.Provinces.Length != 0);
        Debug.Assert(Owner is not null);

        var state = new Node("state");
        var history = new Node("history");
        var child = new List<Child>(7)
        {
            ChildHelper.Leaf("id", Id),
            ChildHelper.LeafQString("name", State.Name),
            ChildHelper.Leaf("manpower", State.Manpower),
            ChildHelper.LeafString("state_category", Category.Name),
            Child.Create(history),
            ChildHelper.Node("provinces", State.Provinces.Select(ChildHelper.LeafValue))
        };
        if (State.IsImpassable)
        {
            child.Add(ChildHelper.Leaf("impassable", true));
        }

        var historyChild = new List<Child>(2 + State.VictoryPoints.Length)
        {
            ChildHelper.LeafString("owner", Owner.Tag),
            ChildHelper.LeafString("add_core_of", Owner.Tag)
        };

        if (!Buildings.IsEmpty)
        {
            historyChild.Add(Child.Create(Buildings.ToNode()));
        }

        historyChild.AddRange(
            State.VictoryPoints.Select(point =>
                ChildHelper.Node(
                    "victory_points",
                    [ChildHelper.LeafValue(point.ProvinceId), ChildHelper.LeafValue(point.Value)]
                )
            )
        );

        history.AllArray = historyChild.ToArray();
        state.AllArray = child.ToArray();

        return CKPrinter.PrettyPrintStatement(state.ToRaw);
    }

    #region Overrides

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is StateInfo other && Equals(other);
    }

    public bool Equals(StateInfo? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id == other.Id;
    }

    public static bool operator ==(StateInfo? left, StateInfo? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StateInfo? left, StateInfo? right)
    {
        return !Equals(left, right);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    #endregion
}
