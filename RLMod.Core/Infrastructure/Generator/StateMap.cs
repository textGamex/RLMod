namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateMap(TmpState state, StateType type)
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
    public IEnumerable<int> Edges => _edges;

    private readonly int[] _edges = [.. state.Adjacencies];

    public bool IsImpassable { get; } = state.IsImpassable;

    public int Id { get; } = state.Id;

    /// <summary>
    /// 计算获取省份的价值。
    /// </summary>
    /// <returns>省份的价值</returns>
    public double GetValue()
    {
        return IsImpassable ? 0 : _stateProperties.Value;
    }

    public StateType StateType => _stateProperties.Type;

    private StateProperty _stateProperties =
        new(state, type, StatePropertyLimit.MaxMaxFactories, StatePropertyLimit.MaxResources);
}
