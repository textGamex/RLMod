using ZLinq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class CountryMap
{
    public CountryType Type;
    public int Id { get; }

    public IEnumerable<int> States => _states;

    public static void SetStateMaps(Dictionary<int, StateMap> stateMaps)
    {
        StateMaps = stateMaps;
    }

    public static Dictionary<int, StateMap> StateMaps { get; private set; } = [];

    private readonly HashSet<int> _states = [];
    private readonly HashSet<int> _border = [];

    public CountryMap(int seed)
    {
        Id = seed;
        AddState(seed);
    }

    /// <summary>
    /// 计算获取国家的价值。
    /// </summary>
    /// <returns>国家的价值</returns>

    public double GetValue() => _states.Sum(s => StateMaps[s].GetValue());

    public List<int> GetPassableBorder() => _border.Where(n => !StateMaps[n].IsImpassable).ToList();

    public int GetStateCount() => _states.Count;

    public int StateCount() => _states.Count;

    public bool ContainsState(int id) => _states.Contains(id);

    public void AddState(int id)
    {
        _states.Add(id);
        UpdateBorders(id);
        UpdateCountryType();
    }

    private void UpdateBorders(int addedState)
    {
        foreach (
            int edge in StateMaps[addedState].Edges.AsValueEnumerable().Where(edge => !_states.Contains(edge))
        )
        {
            _border.Add(edge);
        }

        _border.Remove(addedState);
    }

    private void UpdateCountryType()
    {
        var typeGroups = _states
            .Select(s => StateMaps[s].StateType)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        Type = typeGroups.OrderByDescending(g => g.Value).First().Key switch
        {
            StateType.Industrial => CountryType.Industrial,
            StateType.Resource => CountryType.Resource,
            _ => CountryType.Balanced,
        };
    }
}
