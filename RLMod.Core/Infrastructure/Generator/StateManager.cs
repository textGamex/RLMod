using System.Collections.Frozen;
using MathNet.Numerics.Random;
using MethodTimer;
using Microsoft.Extensions.DependencyInjection;
using RLMod.Core.Helpers;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;
using RLMod.Core.Services;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfoManager
{
    public IEnumerable<StateInfo> States => _stateInfos.Values;
    public int PassableStateCount => _stateInfos.Values.Count(stateInfo => !stateInfo.IsImpassable);

    private readonly FrozenDictionary<int, StateInfo> _stateInfos;

    [Time]
    public StateInfoManager(IReadOnlyList<State> states, IReadOnlyDictionary<int, Province> provinces)
    {
        // var provinceService = App.Current.Services.GetRequiredService<ProvinceService>();
        // var oceanProvinces = provinceService.GetOceanProvinces(provinces);
        var stateInfos = new Dictionary<int, StateInfo>(states.Count);
        var stateAdjacentMap = new Dictionary<int, List<int>>(states.Count);

        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            for (int j = i + 1; j < states.Count; j++)
            {
                var otherState = states[j];
                LookupStateAdjacencies(state, otherState, provinces, stateAdjacentMap);
            }
        }

        var random = RandomHelper.GetRandomWithSeed();
        var stateTypes = random.GetItems(Enum.GetValues<StateType>(), states.Count);
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            stateInfos[state.Id] = new StateInfo(
                state,
                stateAdjacentMap.TryGetValue(state.Id, out var ints) ? ints.ToArray() : [],
                stateTypes[i]
            );
        }

        _stateInfos = stateInfos.ToFrozenDictionary();
    }

    private void LookupStateAdjacencies(
        State state,
        State otherState,
        IReadOnlyDictionary<int, Province> provinces,
        Dictionary<int, List<int>> stateAdjacentMap
    )
    {
        foreach (int provinceId in state.Provinces)
        {
            foreach (int otherStateProvinceId in otherState.Provinces)
            {
                if (
                    !provinces.TryGetValue(otherStateProvinceId, out var otherStateProvince)
                    || !otherStateProvince.Adjacencies.Contains(provinceId)
                )
                {
                    continue;
                }

                if (!stateAdjacentMap.TryGetValue(state.Id, out var adjacentList))
                {
                    adjacentList = new List<int>(4);
                    stateAdjacentMap[state.Id] = adjacentList;
                }
                adjacentList.Add(otherState.Id);

                if (!stateAdjacentMap.TryGetValue(otherState.Id, out var otherStateAdjacentList))
                {
                    otherStateAdjacentList = new List<int>(4);
                    stateAdjacentMap[otherState.Id] = otherStateAdjacentList;
                }
                otherStateAdjacentList.Add(state.Id);
                return;
            }
        }
    }

    public StateInfo GetStateInfo(int stateId)
    {
        return _stateInfos[stateId];
    }
}
