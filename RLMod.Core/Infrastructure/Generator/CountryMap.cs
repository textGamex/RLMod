using ZLinq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class CountryMap
{
    public CountryType Type { get; private set; }
    public int Id { get; }

    public IEnumerable<int> StatesId => _statesId;

    public static IReadOnlyDictionary<int, StateInfo> StateMaps { get; private set; } = [];

    private readonly HashSet<int> _statesId = [];
    private readonly HashSet<int> _border = [];

    public CountryMap(int initialStateId)
    {
        Id = initialStateId;
        AddState(initialStateId);
    }

    public static void SetStateInfos(IReadOnlyDictionary<int, StateInfo> stateMaps)
    {
        StateMaps = stateMaps;
    }

    /// <summary>
    /// 计算获取国家的价值。
    /// </summary>
    /// <returns>国家的价值</returns>
    public double GetValue()
    {
        return _statesId.Sum(id => StateMaps[id].GetValue());
    }

    public IReadOnlyCollection<int> GetPassableBorder()
    {
        return _border.Where(n => !StateMaps[n].IsImpassable).ToArray();
    }

    public int StateCount => _statesId.Count;

    public bool ContainsState(int id) => _statesId.Contains(id);

    public void AddState(int id)
    {
        _statesId.Add(id);
        UpdateBorders(id);
        UpdateCountryType();
    }

    private void UpdateBorders(int addedState)
    {
        Console.WriteLine($"更新{Id}的接壤省份", Id);
        foreach (
            int edge in StateMaps[addedState]
                .Edges.AsValueEnumerable()
                .Where(edge => !_statesId.Contains(edge) && !MapGenerator.OccupiedStates.Contains(edge))
        )
        {
            _border.Add(edge);
        }

        _border.Remove(addedState);
        foreach (int edge in _border.Where(e => MapGenerator.OccupiedStates.Contains(e)))
        {
            _border.Remove(edge);
        }
    }

    private void UpdateCountryType()
    {
        var typeGroups = _statesId
            .Select(s => StateMaps[s].StateType)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        var stateType = typeGroups.OrderByDescending(g => g.Value).First().Key;
        Type = stateType switch
        {
            StateType.Industrial => CountryType.Industrial,
            StateType.Resource => CountryType.Resource,
            _ => CountryType.Balanced,
        };
    }
}
