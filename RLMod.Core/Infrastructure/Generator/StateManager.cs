using MethodTimer;
using RLMod.Core.Helpers;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfoManager
{
    public IEnumerable<StateInfo> States => _stateInfos;
    public int PassableStateCount => _stateInfos.Count(stateInfo => !stateInfo.IsImpassable);
    public int PassableLandStateCount =>
        _stateInfos.Count(stateInfo => !stateInfo.IsImpassable && !stateInfo.IsOcean);

    private readonly StateInfo[] _stateInfos;

    [Time]
    public StateInfoManager(IReadOnlyList<State> states, IReadOnlyDictionary<int, Province> provinces)
    {
        var stateInfos = new List<StateInfo>(states.Count);
        var stateAdjacentMap = new Dictionary<int, List<StateInfo>>(states.Count);

        var random = RandomHelper.GetRandomWithSeed();
        var stateTypes = random.GetItems(Enum.GetValues<StateType>(), states.Count);
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            stateInfos.Add(new StateInfo(state, stateTypes[i]));
        }

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
