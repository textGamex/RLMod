using ZLinq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class CountryInfo
{
    public string Tag { get; }
    public CountryType Type { get; private set; }
    public int Id { get; }

    public IEnumerable<int> StatesId => _statesId;

    public static StateInfoManager StateInfoManager { get; private set; } = null!;

    private readonly HashSet<int> _statesId = [];
    private readonly HashSet<int> _border = [];

    // TODO: _statesId 是否应该替换为 StateInfo 类?
    public CountryInfo(int initialStateId, string tag)
    {
        Tag = tag;
        Id = initialStateId;
        AddState(initialStateId);
    }

    public static void SetStateInfoManager(StateInfoManager stateInfoManager)
    {
        StateInfoManager = stateInfoManager;
    }

    /// <summary>
    /// 计算获取国家的价值。
    /// </summary>
    /// <returns>国家的价值</returns>
    public double GetValue()
    {
        return _statesId.Sum(id => StateInfoManager.GetStateInfo(id).Value);
    }

    public IReadOnlyCollection<int> GetPassableBorder()
    {
        return _border.Where(id => !StateInfoManager.GetStateInfo(id).IsImpassable).ToArray();
    }

    public int StateCount => _statesId.Count;

    public bool ContainsState(int id) => _statesId.Contains(id);

    public void AddState(int id)
    {
        _statesId.Add(id);
        UpdateBorders(id);
        UpdateCountryType();
    }

    private void UpdateBorders(int addedStateId)
    {
        Console.WriteLine($"更新{Id}的接壤省份", Id);
        foreach (
            int edge in StateInfoManager
                .GetStateInfo(addedStateId)
                .Edges.AsValueEnumerable()
                .Where(edge => !_statesId.Contains(edge) && !MapGenerator.OccupiedStates.Contains(edge))
        )
        {
            _border.Add(edge);
        }

        _border.Remove(addedStateId);
        foreach (int edge in _border.Where(e => MapGenerator.OccupiedStates.Contains(e)))
        {
            _border.Remove(edge);
        }
    }

    private void UpdateCountryType()
    {
        var typeGroups = _statesId
            .Select(stateId => StateInfoManager.GetStateInfo(stateId).Type)
            .GroupBy(type => type)
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
