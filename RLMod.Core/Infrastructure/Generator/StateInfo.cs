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

/// <summary>
/// 生成地图是临时用于数据处理的省份（State）类。
/// </summary>
public sealed class StateInfo : IEquatable<StateInfo>
{
    /// <summary>
    /// 省份的唯一标识符。
    /// 当 Id &lt; 0 时为 海洋省份（OceanState）。
    /// </summary>
    public int Id { get; }
    public CountryInfo? Owner { get; set; }

    /// <summary>
    /// 存储的省份（State）数据。
    /// </summary>
    public State State { get; }

    /// <summary>
    /// 省份（State）工业值。
    /// </summary>
    public int FactorySum { get; set; }

    /// <summary>
    /// 省份（State）资源值。
    /// </summary>
    public int ResourceSum { get; set; }

    /// <summary>
    /// 相邻省份（State）表。
    /// </summary>
    public IEnumerable<StateInfo> AdjacentStates => _adjacentStates;

    /// <summary>
    /// 省份（State）发展类型。
    /// </summary>
    public StateType Type { get; }

    /// <summary>
    /// 省份发展度（StateCategory）。
    /// </summary>
    public StateCategory Category { get; }

    /// <summary>
    /// 是否不可通行（Impassable）。
    /// </summary>
    public bool IsImpassable { get; }

    /// <summary>
    /// 是否为海洋。
    /// </summary>
    public bool IsOcean { get; }

    /// <summary>
    /// 是否临海。
    /// </summary>
    public bool IsCoastal { get; }

    /// <summary>
    /// 是否为可通行陆地。
    /// </summary>
    public bool IsPassableLand => !IsImpassable && !IsOcean;

    /// <summary>
    /// 最大工业值。
    /// </summary>
    public int MaxFactories { get; }

    /// <summary>
    /// 总胜利点（VictoryPoint）。
    /// </summary>
    public int TotalVictoryPoint { get; }

    /// <summary>
    /// 省份建筑。
    /// </summary>
    public StateBuildings Buildings { get; } = new();

    public StateResource Resources { get; } = new();

    /// <summary>
    /// 省份（State）的价值。
    /// </summary>
    public double Value => IsImpassable || IsOcean ? 0 : GetValue();

    /// <summary>
    /// 根据价值公式计算省份（State）价值
    /// </summary>
    /// <returns>省份（State）价值</returns>
    private double GetValue()
    {
        // 加权百分制计算价值
        return (double)FactorySum
                / AppSettingService.StateGenerate.MaxFactoryNumber
                * 100
                * AppSettingService.StateGenerate.FactoryNumberWeight
            + (double)MaxFactories
                / AppSettingService.StateGenerate.MaxFactoryNumber
                * 100
                * AppSettingService.StateGenerate.MaxFactoryNumberWeight
            + (double)ResourceSum
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

    /// <summary>
    /// 用于分配海洋省份（OceanState）Id 的静态变量。
    /// </summary>
    private static int _oceanStateId;

    private static readonly StateCategoryService StateCategoryService =
        App.Current.Services.GetRequiredService<StateCategoryService>();
    private static readonly AppSettingService AppSettingService =
        App.Current.Services.GetRequiredService<AppSettingService>();
    private static readonly BuildingGenerateSettingService BuildingGenerateSettingService =
        App.Current.Services.GetRequiredService<BuildingGenerateSettingService>();
    private static readonly BuildingService BuildingService =
        App.Current.Services.GetRequiredService<BuildingService>();
    private static readonly ResourceGenerateSettingService ResourceGenerateSettingService =
        App.Current.Services.GetRequiredService<ResourceGenerateSettingService>();

    /// <summary>
    /// 陆地省份构造函数。
    /// </summary>
    /// <param name="state">省份</param>
    /// <param name="isCoastal">是否临海</param>
    /// <param name="type">类型</param>
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
        // 随机生成省份发展度
        int minFactories;
        int maxFactories;
        switch (Type)
        {
            case StateType.Industrial:
                minFactories = (int)(0.70 * maxFactoriesLimit);
                maxFactories = (int)(1.0 * maxFactoriesLimit);
                break;
            case StateType.Resource:
                minFactories = (int)(0.0 * maxFactoriesLimit);
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
        // 生成工业值与资源值
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

    /// <summary>
    /// 根据工业最大值范围随机合适的省份发展度
    /// </summary>
    /// <param name="minSlots">最小值</param>
    /// <param name="maxSlots">最大值</param>
    /// <returns>省份发展度</returns>
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

    /// <summary>
    /// 生成工业型省份属性
    /// </summary>
    /// <param name="maxFactories">最大工厂值</param>
    /// <param name="maxResources">最大资源值</param>
    private void GenerateIndustrialProperties(int maxFactories, int maxResources)
    {
        FactorySum = _random.Next((int)(maxFactories * 0.5), (int)(maxFactories * 0.7) + 1);

        int resourceMax = _random.Next(FactorySum * 10, (int)(maxResources * 0.3) + 1);
        ResourceSum = _random.Next(0, resourceMax + 1);
    }

    /// <summary>
    /// 生成资源型省份属性
    /// </summary>
    /// <param name="maxFactories">最大工厂值</param>
    /// <param name="maxResources">最大资源值</param>
    private void GenerateResourceProperties(int maxFactories, int maxResources)
    {
        ResourceSum = _random.Next((int)(maxResources * 0.7), maxResources + 1);

        int factoryMax = Math.Min((int)(ResourceSum * 0.005), (int)(maxFactories * 0.7));
        FactorySum = _random.Next(0, factoryMax + 1);
    }

    /// <summary>
    /// 生成平衡型省份属性
    /// </summary>
    /// <param name="maxFactories">最大工厂值</param>
    /// <param name="maxResources">最大资源值</param>
    private void GenerateBalancedProperties(int maxFactories, int maxResources)
    {
        FactorySum = _random.Next((int)(maxFactories * 0.3), (int)(maxFactories * 0.7) + 1);
        ResourceSum = _random.Next((int)(maxResources * 0.3), (int)(maxResources * 0.7) + 1);

        double resourceStandard = FactorySum * 50.0;
        while (Math.Abs(ResourceSum - resourceStandard) > 50)
        {
            ResourceSum =
                (int)(resourceStandard + _random.Next(26)) * (ResourceSum > resourceStandard ? 1 : -1);
            ResourceSum = Math.Max(0, Math.Min(maxResources, ResourceSum));
        }
    }

    /// <summary>
    /// 生成建筑
    /// </summary>
    public void GenerateBuildings()
    {
        // 将替换生成类型放到最后处理
        foreach (
            var setting in BuildingGenerateSettingService
                .Settings.AsValueEnumerable()
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
                Buildings.Add(setting.Name, (int)Math.Round(FactorySum * setting.Proportion));
            }
        }
    }

    public void GenerateResources()
    {
        foreach (var setting in ResourceGenerateSettingService.Settings)
        {
            int amount = (int)Math.Round(ResourceSum * setting.Proportion);
            Resources.Add(setting.Name, amount);
        }
    }

    public string ToScript()
    {
        Debug.Assert(State.Provinces.Length != 0);
        Debug.Assert(Owner is not null);

        var state = new Node("state");
        var history = new Node("history");
        var child = new List<Child>(8)
        {
            ChildHelper.Leaf("id", Id),
            ChildHelper.LeafQString("name", State.Name),
            ChildHelper.Leaf("manpower", State.Manpower),
            ChildHelper.LeafString("state_category", Category.Name),
            Child.Create(history),
            ChildHelper.Node("provinces", State.Provinces.Select(ChildHelper.LeafValue)),
        };
        if (State.IsImpassable)
        {
            child.Add(ChildHelper.Leaf("impassable", true));
        }

        if (!Resources.IsEmpty)
        {
            child.Add(Child.Create(Resources.ToNode()));
        }

        var historyChild = new List<Child>(2 + State.VictoryPoints.Length)
        {
            ChildHelper.LeafString("owner", Owner.Tag),
            ChildHelper.LeafString("add_core_of", Owner.Tag),
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
