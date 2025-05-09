using MethodTimer;
using NLog;
using RLMod.Core.Helpers;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfoManager
{
    public IReadOnlyList<StateInfo> States => _stateInfos;
    public int PassableLandStateCount => _stateInfos.Count(stateInfo => stateInfo.IsPassableLand);

    private readonly StateInfo[] _stateInfos;

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    [Time]
    public StateInfoManager(
        IReadOnlyList<State> states,
        IReadOnlyDictionary<int, Province> provinces,
        IReadOnlyCollection<IEnumerable<int>> oceanStates
    )
    {
        StateInfo.ResetOceanStateId();
        var stateInfos = new List<StateInfo>(states.Count + oceanStates.Count);
        var stateAdjacentMap = new Dictionary<int, List<StateInfo>>(states.Count);

        var random = RandomHelper.GetRandomWithSeed();
        var stateTypes = random.GetItems(Enum.GetValues<StateType>(), states.Count);
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];

            bool isCoastal = state.Provinces.Any(province =>
                provinces.TryGetValue(province, out var value) && value.IsCoastal
            );
            stateInfos.Add(new StateInfo(state, isCoastal, stateTypes[i]));
        }
        Log.Debug("Read {Count} states...", states.Count);
        foreach (var oceanState in oceanStates)
        {
            stateInfos.Add(new StateInfo(oceanState.ToArray()));
        }
        Log.Debug("Read {Count} oceanstates...", oceanStates.Count);
        StateInfo.ResetOceanStateId();

        // 查找相邻的State
        for (int i = 0; i < stateInfos.Count; i++)
        {
            var state = stateInfos[i];
            for (int j = i + 1; j < stateInfos.Count; j++)
            {
                var otherState = stateInfos[j];
                LookupStateAdjacencies(state, otherState, provinces, stateAdjacentMap);
            }
        }

        foreach (var stateInfo in stateInfos)
        {
            if (stateAdjacentMap.TryGetValue(stateInfo.Id, out var value))
            {
                //TODO: 是否应该移除这个 ToArray?
                stateInfo.SetAdjacent(value.ToArray());
            }
        }

        _stateInfos = stateInfos.ToArray();
    }

    private void LookupStateAdjacencies(
        StateInfo stateInfo,
        StateInfo otherStateInfo,
        IReadOnlyDictionary<int, Province> provinces,
        Dictionary<int, List<StateInfo>> stateAdjacentMap
    )
    {
        foreach (int provinceId in stateInfo.State.Provinces)
        {
            foreach (int otherStateProvinceId in otherStateInfo.State.Provinces)
            {
                if (
                    !provinces.TryGetValue(otherStateProvinceId, out var otherStateProvince)
                    || !otherStateProvince.Adjacencies.Contains(provinceId)
                )
                {
                    continue;
                }

                if (!stateAdjacentMap.TryGetValue(stateInfo.Id, out var adjacentList))
                {
                    adjacentList = new List<StateInfo>(4);
                    stateAdjacentMap[stateInfo.Id] = adjacentList;
                }
                adjacentList.Add(otherStateInfo);

                if (!stateAdjacentMap.TryGetValue(otherStateInfo.Id, out var otherStateAdjacentList))
                {
                    otherStateAdjacentList = new List<StateInfo>(4);
                    stateAdjacentMap[otherStateInfo.Id] = otherStateAdjacentList;
                }
                otherStateAdjacentList.Add(stateInfo);
                return;
            }
        }
    }
}
