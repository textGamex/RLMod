using System.Collections.Frozen;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateInfoManager
{
    public IEnumerable<StateInfo> States => _stateInfos.Values;
    private readonly FrozenDictionary<int, StateInfo> _stateInfos;

    public StateInfoManager(IReadOnlyCollection<TmpState> states)
    {
        var stateInfos = new Dictionary<int, StateInfo>(states.Count);

        var stateTypes = Random.Shared.GetItems(Enum.GetValues<StateType>(), states.Count);
        int i = 0;
        foreach (var state in states)
        {
            stateInfos[state.Id] = new StateInfo(state, stateTypes[i++]);
        }

        _stateInfos = stateInfos.ToFrozenDictionary();
    }

    public StateInfo GetStateInfo(int stateId)
    {
        return _stateInfos[stateId];
    }

    public int PassableStateCount => _stateInfos.Values.Count(stateInfo => !stateInfo.IsImpassable);
}
