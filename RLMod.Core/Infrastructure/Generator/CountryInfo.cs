using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using RLMod.Core.Services;
using ZLinq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class CountryInfo
{
    public string Tag { get; }
    public CountryType Type { get; private set; }
    public int InitialId { get; }

    public IReadOnlyCollection<StateInfo> States => _states;

    public void ClearOceanStates()
    {
        foreach (var stateInfo in _states.AsValueEnumerable().Where(stateInfo => stateInfo.IsOcean))
        {
            _states.Remove(stateInfo);
        }
    }

    private readonly HashSet<StateInfo> _states = [];
    private readonly HashSet<StateInfo> _borders = [];

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public CountryInfo(StateInfo initialState, string tag)
    {
        Tag = tag;
        InitialId = initialState.Id;
        AddState(initialState);
    }

    /// <summary>
    /// 计算获取国家的价值。
    /// </summary>
    /// <returns>国家的价值</returns>
    public double GetValue()
    {
        return _states.Sum(state => state.Value);
    }

    /// <summary>
    /// 获取非不可通行（IsImpassable）的相邻省份（State）。
    /// </summary>
    /// <returns>相邻省份（State）</returns>
    public IReadOnlyCollection<StateInfo> GetPassableBorder()
    {
        return _borders.AsValueEnumerable().Where(state => !state.IsImpassable).ToArray();
    }

    public bool ContainsState(StateInfo state) => _states.Contains(state);

    public void AddState(StateInfo state)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(Tag));
        Debug.Assert(!_states.Contains(state));

        state.Owner = this;
        _states.Add(state);
        UpdateBorders(state);
        UpdateCountryType();
    }

    private void UpdateBorders(StateInfo addedState)
    {
        foreach (
            var edgeState in addedState.AdjacentStates.AsValueEnumerable().Where(s => !_states.Contains(s))
        )
        {
            _borders.Add(edgeState);
        }
        _borders.Remove(addedState);
        foreach (var state in _borders.AsValueEnumerable().Where(s => _states.Contains(s)))
        {
            _borders.Remove(state);
        }
    }

    private void UpdateCountryType()
    {
        // TODO: 优化
        var typeGroups = _states
            .AsValueEnumerable()
            .Select(stateId => stateId.Type)
            .GroupBy(type => type)
            .ToDictionary(g => g.Key, g => g.Count());

        var stateType = typeGroups.AsValueEnumerable().OrderByDescending(g => g.Value).First().Key;
        Type = stateType switch
        {
            StateType.Industrial => CountryType.Industrial,
            StateType.Resource => CountryType.Resource,
            _ => CountryType.Balanced,
        };
    }

    /// <summary>
    /// 生成 State 上的建筑和资源, 应在所有 State 完成分配后调用.
    /// </summary>
    public void GenerateStatesBuildingsAndResources()
    {
        foreach (var state in _states)
        {
            state.GenerateBuildings();
            state.GenerateResources();
        }
    }

    public void WriteToFiles()
    {
        var settings = App.Current.Services.GetRequiredService<AppSettingService>();

        string statesFolder = Path.Combine(settings.OutputFolderPath, App.ModName, "history", "states");
        foreach (var state in States)
        {
            string path = Path.Combine(statesFolder, $"{state.Id}.txt");
            File.WriteAllText(path, state.ToScript(), App.Utf8WithoutBom);
        }
    }
}
