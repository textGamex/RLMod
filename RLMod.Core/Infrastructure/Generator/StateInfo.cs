namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfo(
    int id,
    int[] adjacent,
    bool isImpassable,
    int totalVictoryPoint,
    StateType type
)
{
    public int Factories
    {
        get => _stateProperties.Factories;
        set => _stateProperties.Factories = value;
    }

    public int Resources
    {
        get => _stateProperties.Resources;
        set => _stateProperties.Resources = value;
    }
    public IEnumerable<int> Edges => adjacent;

    public bool IsImpassable { get; } = isImpassable;

    public int Id { get; } = id;

    /// <summary>
    /// 计算获取省份的价值。
    /// </summary>
    /// <returns>省份的价值</returns>
    public double GetValue()
    {
        return IsImpassable ? 0 : _stateProperties.Value;
    }

    public StateType StateType => _stateProperties.Type;

    public StateProperty GetProperties() => _stateProperties;

    private readonly StateProperty _stateProperties =
        new(totalVictoryPoint, type, StatePropertyLimit.MaxMaxFactories, StatePropertyLimit.MaxResources);
}
