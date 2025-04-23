using MathNet.Numerics.Random;
using Microsoft.Extensions.DependencyInjection;
using RLMod.Core.Helpers;
using RLMod.Core.Models.Map;
using RLMod.Core.Services;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfo : IEquatable<StateInfo>
{
    public int Id => State.Id;
    public string Owner { get; set; } = string.Empty;
    public State State { get; }
    public int Factories { get; set; }
    public int Resources { get; set; }
    public IEnumerable<StateInfo> Edges => _adjacent;
    public StateType Type { get; }
    public bool IsImpassable { get; }
    public bool IsOcean { get; }
    public bool IsPassableLand => !IsImpassable && !IsOcean;
    public int MaxFactories { get; }
    public int TotalVictoryPoint { get; }

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

    private StateInfo[] _adjacent = [];
    private readonly MersenneTwister _random;

    private static readonly StateCategoryService StateCategoryService =
        App.Current.Services.GetRequiredService<StateCategoryService>();
    private static readonly AppSettingService AppSettingService =
        App.Current.Services.GetRequiredService<AppSettingService>();

    public StateInfo(State state, StateType type)
    {
        _random = RandomHelper.GetRandomWithSeed();

        State = state;
        Type = type;
        IsImpassable = state.IsImpassable;
        TotalVictoryPoint = state.VictoryPoints.Sum(point => point.Value);

        int maxFactoriesLimit = AppSettingService.StateGenerate.MaxFactoryNumber;

        switch (Type)
        {
            case StateType.Industrial:
                MaxFactories = GetRandomBuildingSlots(
                    (int)(0.70 * maxFactoriesLimit),
                    (int)(1.0 * maxFactoriesLimit)
                );
                break;
            case StateType.Resource:
                MaxFactories = GetRandomBuildingSlots(
                    (int)(0.1 * maxFactoriesLimit),
                    (int)(0.3 * maxFactoriesLimit)
                );
                break;
            case StateType.Balanced:
            default:
                MaxFactories = GetRandomBuildingSlots(
                    (int)(0.3 * maxFactoriesLimit),
                    (int)(0.7 * maxFactoriesLimit)
                );
                break;
        }

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

    public void SetAdjacent(StateInfo[] adjacent)
    {
        _adjacent = adjacent;
    }

    private int GetRandomBuildingSlots(int minSlots, int maxSlots)
    {
        var slots = new List<int>(8);
        foreach (var stateCategory in StateCategoryService.StateCategories)
        {
            if (stateCategory.Slots >= minSlots && stateCategory.Slots <= maxSlots)
            {
                slots.Add(stateCategory.Slots);
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
}
