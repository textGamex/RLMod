using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using MethodTimer;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using RLMod.Core.Extensions;
using RLMod.Core.Helpers;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;
using RLMod.Core.Services;
using ZLinq;
using ZLinq.Linq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class MapGenerator
{
    public static IReadOnlySet<StateInfo> OccupiedStates => _occupiedStates;

    /// <summary>
    /// 已经被分配的 States
    /// </summary>
    private static readonly HashSet<StateInfo> _occupiedStates = [];

    private readonly StateInfoManager _stateInfoManager;
    private readonly int _countriesCount;
    private readonly MersenneTwister _random;
    private readonly double _valueMean;
    private readonly double _valueStdDev;
    private readonly Dictionary<(StateInfo, int), int> _pathCache = new();
    private readonly AppSettingService _settings;
    private readonly CountryTagService _countryTagService;

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public MapGenerator(
        IReadOnlyList<State> states,
        int countriesCount = MapSettings.MaxCountry,
        double valueMean = 5000,
        double valueStdDev = 1000
    )
    {
        _countryTagService = App.Current.Services.GetRequiredService<CountryTagService>();
        _settings = App.Current.Services.GetRequiredService<AppSettingService>();
        _countriesCount = countriesCount;
        _valueMean = valueMean;
        _valueStdDev = valueStdDev;

        if (!ProvinceParser.TryParse(_settings.GameRootFolderPath, out var provinces))
        {
            throw new ArgumentException("Could not parse province file");
        }

        _stateInfoManager = new StateInfoManager(states, provinces);
        _random = RandomHelper.GetRandomWithSeed();
        ValidateStateCount();
    }

    private void ValidateStateCount()
    {
        int passableStateCount = _stateInfoManager.PassableStateCount;
        if (passableStateCount < _countriesCount)
        {
            throw new ArgumentException(
                $"无法生成 {_countriesCount} 个国家，非海洋省份只有 {passableStateCount} 个。请确保：(非海洋省份数量) ≥ (目标国家数量)"
            );
        }
    }

    [Time]
    public IReadOnlyCollection<CountryInfo> GenerateRandomCountry()
    {
        Log.Info("选择初始位置...");

        var countryTags = _countryTagService.GetCountryTags().ToList();

        var countries = GetRandomInitialState()
            .Select(initialState =>
            {
                int index = _random.Next(countryTags.Count);
                string countryTag = countryTags[index];
                countryTags.RemoveFastAt(index);
                return new CountryInfo(initialState, countryTag);
            })
            .ToArray();

        Log.Info("初始位置分配完毕...");

        bool isChange;
        do
        {
            isChange = false;

            foreach (var country in countries)
            {
                Log.Debug("尝试扩展...{Id}", country.InitialId);
                var passableBorder = country.GetPassableBorder();
                if (passableBorder.Count <= 0)
                {
                    Log.Debug("无法扩展");
                    continue;
                }

                isChange = ExpandCountry(country, passableBorder, countries);
            }
        } while (isChange);

        Log.Info("扩展完毕");
        Log.Info("正则验证");
        ApplyValueDistribution(countries);
        return countries;
    }

    private bool ExpandCountry(
        CountryInfo country,
        IReadOnlyCollection<StateInfo> candidates,
        CountryInfo[] countries
    )
    {
        var state = GetIdOfBestState(candidates, countries);
        if (state is null)
        {
            return false;
        }

        Log.Debug("向{StateId}扩展", state.Id);
        country.AddState(state);
        _occupiedStates.Add(state);
        return true;
    }

    private ValueEnumerable<
        Select<
            OrderBySkipTake<Where<FromEnumerable<StateInfo>, StateInfo>, StateInfo, int>,
            StateInfo,
            StateInfo
        >,
        StateInfo
    > GetRandomInitialState()
    {
        return _stateInfoManager
            .States.AsValueEnumerable()
            .Where(s => !s.IsImpassable && !_occupiedStates.Contains(s))
            .OrderBy(_ => _random.Next())
            .Take(_countriesCount)
            .Select(s =>
            {
                //TODO: 优化
                _occupiedStates.Add(s);
                return s;
            });
    }

    private StateInfo? GetIdOfBestState(IReadOnlyCollection<StateInfo> candidates, CountryInfo[] countries)
    {
        var validCandidates = candidates.Where(id => !_occupiedStates.Contains(id)).ToList();
        if (validCandidates.Count == 0)
        {
            return null;
        }

        var scores = candidates
            .AsValueEnumerable()
            .Select(stateInfo => new
            {
                State = stateInfo,
                Dispersion = CalculateDispersion(stateInfo, countries),
                TypeMatch = CalculateTypeMatch(stateInfo, countries),
            })
            .ToArray();

        var maxValues = new
        {
            Value = scores.Max(s => s.State.Value),
            Dispersion = scores.Max(s => s.Dispersion),
            TypeMatch = scores.Max(s => s.TypeMatch),
        };

        return scores
            .AsValueEnumerable()
            .Select(stateInfo => new
            {
                StateInfo = stateInfo,
                Score = 0.5 * (stateInfo.State.Value / maxValues.Value)
                    + 0.3 * (stateInfo.Dispersion / maxValues.Dispersion)
                    + 0.2 * (stateInfo.TypeMatch / maxValues.TypeMatch)
            })
            .OrderByDescending(s => s.Score)
            .First()
            .StateInfo.State;
    }

    private double CalculateDispersion(StateInfo state, CountryInfo[] countries)
    {
        int sumDistance = countries
            .AsValueEnumerable()
            .Where(c => c.InitialId != state.Id)
            .Sum(c => ShortestPathLengthBfs(state, c.InitialId));

        return (double)sumDistance / (countries.Length - 1);
    }

    private static double CalculateTypeMatch(StateInfo state, CountryInfo[] countries)
    {
        var targetType = state.Type;
        return countries
            .AsValueEnumerable()
            .Where(c => c.GetPassableBorder().Contains(state))
            .Average(countryMap => countryMap.Type.EqualsForType(targetType) ? 1 : 0);
    }

    private int ShortestPathLengthBfs(StateInfo startState, int endStateId)
    {
        if (_pathCache.TryGetValue((startState, endStateId), out int cached))
        {
            return cached;
        }

        var visited = new HashSet<int>();
        var queue = new Queue<(StateInfo, int distance)>();
        queue.Enqueue((startState, 0));
        while (queue.Count > 0)
        {
            var (currentState, distance) = queue.Dequeue();
            if (currentState.Id == endStateId)
            {
                _pathCache[(startState, endStateId)] = distance;
                return distance;
            }
            if (!visited.Add(currentState.Id))
            {
                continue;
            }

            foreach (
                var edgeState in currentState
                    .Edges.AsValueEnumerable()
                    .Where(state => !state.IsImpassable && !visited.Contains(state.Id))
            )
            {
                queue.Enqueue((edgeState, distance + 1));
            }
        }
        _pathCache[(startState, endStateId)] = -1;
        return -1;
    }

    private void ApplyValueDistribution(IReadOnlyCollection<CountryInfo> countries)
    {
        double[] targetValues = GenerateNormalDistribution(countries.Count);
        Array.Sort(targetValues);

        var orderedCountries = countries.OrderBy(c => c.GetValue()).ToArray();

        for (int i = 0; i < orderedCountries.Length; i++)
        {
            var country = orderedCountries[i];
            double currentValue = country.GetValue();
            double targetValue = targetValues[i];

            double ratio = targetValue / currentValue;
            if (double.IsNaN(ratio) || ratio is >= 0.95 and <= 1.05)
            {
                continue;
            }

            foreach (var state in country.States)
            {
                if (state.IsImpassable)
                {
                    continue;
                }

                AdjustStateProperties(state, ratio);
            }
        }
    }

    private void AdjustStateProperties(StateInfo state, double ratio)
    {
        const double industrialFactoryMinRatio = 0.8;
        const double industrialResourceMaxRatio = 1.0;
        const double resourceResourceMinRatio = 0.8;

        var type = state.Type;
        int originalFactories = state.Factories;
        int originalResources = state.Resources;

        if (type == StateType.Industrial)
        {
            double factoryRatio = ratio * 1.2;
            factoryRatio = Math.Max(industrialFactoryMinRatio, Math.Min(1.2, factoryRatio));
            state.Factories = ClampValue(
                (int)(originalFactories * factoryRatio),
                min: (int)(originalFactories * industrialFactoryMinRatio),
                max: _settings.StateGenerate.MaxFactoryNumber
            );

            double resourceRatio = ratio * 0.8;
            resourceRatio = Math.Min(industrialResourceMaxRatio, resourceRatio);
            state.Resources = ClampValue(
                (int)(originalResources * resourceRatio),
                max: _settings.StateGenerate.MaxResourceNumber
            );
        }
        else if (type == StateType.Resource)
        {
            double resourceRatio = ratio * 1.2;
            resourceRatio = Math.Max(resourceResourceMinRatio, Math.Min(1.2, resourceRatio));
            state.Resources = ClampValue(
                (int)(originalResources * resourceRatio),
                min: (int)(originalResources * resourceResourceMinRatio),
                max: _settings.StateGenerate.MaxResourceNumber
            );

            state.Factories = ClampValue(
                (int)(originalFactories * ratio * 0.8),
                max: _settings.StateGenerate.MaxFactoryNumber
            );
        }
        else
        {
            double factoryRatio = ratio * (0.9 + _random.NextDouble() * 0.2); // 0.9-1.1
            double resourceRatio = ratio * (0.9 + _random.NextDouble() * 0.2);

            state.Factories = ClampValue(
                (int)(originalFactories * factoryRatio),
                min: (int)(originalFactories * 0.7),
                max: _settings.StateGenerate.MaxFactoryNumber
            );
            state.Resources = ClampValue(
                (int)(originalResources * resourceRatio),
                min: (int)(originalResources * 0.7),
                max: _settings.StateGenerate.MaxResourceNumber
            );
        }
    }

    //
    // private void RebalanceProperties(StateMap state)
    // {
    //     switch (state.StateType)
    //     {
    //         case StateType.Industrial:
    //             state.Resources = (int)(state.Factories * 0.4);
    //             break;
    //         case StateType.Resource:
    //             state.Factories = (int)(state.Resources * 0.3);
    //             break;
    //         default:
    //             state.Resources = Math.Min(state.Factories * 24, StatePropertyLimit.MaxResources);
    //             break;
    //     }
    // }

    private static int ClampValue(int value, int min = 0, int max = int.MaxValue) =>
        Math.Max(min, Math.Min(value, max));

    private double[] GenerateNormalDistribution(int count)
    {
        var normal = Normal.WithMeanStdDev(_valueMean, _valueStdDev, _random);
        double[] values = new double[count];
        normal.Samples(values);
        return values;
    }
}
