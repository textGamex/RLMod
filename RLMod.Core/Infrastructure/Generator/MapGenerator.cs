using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using RLMod.Core.Extensions;
using RLMod.Core.Infrastructure.Parser;
using RLMod.Core.Models.Map;
using RLMod.Core.Services;
using ZLinq;
using ZLinq.Linq;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class MapGenerator
{
    public static IReadOnlySet<int> OccupiedStates => _occupiedStates;
    private static readonly HashSet<int> _occupiedStates = [];

    private readonly StateInfoManager _stateInfoManager;
    private readonly int _countriesCount;
    private readonly Random _random;
    private readonly double _valueMean;
    private readonly double _valueStdDev;
    private readonly Dictionary<(int, int), int> _pathCache = new();
    private readonly AppSettingService _settings;

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public MapGenerator(
        IReadOnlyList<State> states,
        int countriesCount = MapSettings.MaxCountry,
        int randomSeed = MapSettings.RandomSeed,
        double valueMean = 5000,
        double valueStdDev = 1000
    )
    {
        _settings = App.Current.Services.GetRequiredService<AppSettingService>();
        string provinceFilePath = Path.Combine(_settings.GameRootFolderPath, "map", "provinces.bmp");
        string definitionFilePath = Path.Combine(_settings.GameRootFolderPath, "map", "definition.csv");

        if (!ProvinceParser.TryParse(provinceFilePath, definitionFilePath, out var provinces))
        {
            throw new ArgumentException($"Could not parse province file: {provinceFilePath}");
        }

        _stateInfoManager = new StateInfoManager(states, provinces);
        _random = new MersenneTwister(randomSeed);

        _countriesCount = countriesCount;
        _valueMean = valueMean;
        _valueStdDev = valueStdDev;
        CountryMap.SetStateInfoManager(_stateInfoManager);
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

    public IReadOnlyCollection<CountryMap> GenerateRandomCountry()
    {
        Log.Info("选择初始位置...");
        var countries = GetRandomInitialStateId()
            .Select(initialStateId => new CountryMap(initialStateId))
            .ToArray();
        Log.Info("初始位置分配完毕...");

        bool isChange;
        do
        {
            isChange = false;

            foreach (var country in countries)
            {
                Log.Debug("尝试扩展...{Id}", country.Id);
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
        CountryMap country,
        IReadOnlyCollection<int> candidates,
        CountryMap[] countries
    )
    {
        int stateId = GetIdOfBestState(candidates, countries);
        if (stateId == -1)
        {
            Log.Debug("无法扩展");
            return false;
        }

        Log.Debug("向{StateId}扩展", stateId);
        country.AddState(stateId);
        _occupiedStates.Add(stateId);
        return true;
    }

    private ValueEnumerable<
        Select<OrderBySkipTake<Where<FromEnumerable<StateInfo>, StateInfo>, StateInfo, int>, StateInfo, int>,
        int
    > GetRandomInitialStateId()
    {
        return _stateInfoManager
            .States.AsValueEnumerable()
            .Where(s => !s.IsImpassable && !OccupiedStates.Contains(s.Id))
            .OrderBy(_ => _random.Next())
            .Take(_countriesCount)
            .Select(s =>
            {
                _occupiedStates.Add(s.Id);
                return s.Id;
            });
    }

    private int GetIdOfBestState(IReadOnlyCollection<int> candidates, CountryMap[] countries)
    {
        var validCandidates = candidates.Where(id => !OccupiedStates.Contains(id)).ToList();
        if (validCandidates.Count == 0)
        {
            return -1;
        }

        var scores = candidates
            .AsValueEnumerable()
            .Select(id => new
            {
                Id = id,
                _stateInfoManager.GetStateInfo(id).Value,
                Dispersion = CalculateDispersion(id, countries),
                TypeMatch = CalculateTypeMatch(id, countries),
            })
            .ToArray();

        var maxValues = new
        {
            Value = scores.Max(s => s.Value),
            Dispersion = scores.Max(s => s.Dispersion),
            TypeMatch = scores.Max(s => s.TypeMatch),
        };

        return scores
            .AsValueEnumerable()
            .Select(s => new
            {
                s.Id,
                Score = 0.5 * (s.Value / maxValues.Value)
                    + 0.3 * (s.Dispersion / maxValues.Dispersion)
                    + 0.2 * (s.TypeMatch / maxValues.TypeMatch),
            })
            .OrderByDescending(s => s.Score)
            .First()
            .Id;
    }

    private double CalculateDispersion(int id, CountryMap[] countries)
    {
        int sumDistance = countries
            .AsValueEnumerable()
            .Where(c => c.Id != id)
            .Sum(c => ShortestPathLengthBfs(id, c.Id));

        return (double)sumDistance / (countries.Length - 1);
    }

    private double CalculateTypeMatch(int id, CountryMap[] countries)
    {
        var targetType = _stateInfoManager.GetStateInfo(id).Type;
        return countries
            .AsValueEnumerable()
            .Where(c => c.GetPassableBorder().Contains(id))
            .Average(countryMap => countryMap.Type.EqualsForType(targetType) ? 1 : 0);
    }

    private int ShortestPathLengthBfs(int start, int end)
    {
        if (_pathCache.TryGetValue((start, end), out int cached))
        {
            return cached;
        }

        var visited = new HashSet<int>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var (currentStateId, distance) = queue.Dequeue();
            if (currentStateId == end)
            {
                _pathCache[(start, end)] = distance;
                return distance;
            }
            if (!visited.Add(currentStateId))
            {
                continue;
            }

            foreach (
                int edge in _stateInfoManager
                    .GetStateInfo(currentStateId)
                    .Edges.AsValueEnumerable()
                    .Where(edgeStateId =>
                        !_stateInfoManager.GetStateInfo(edgeStateId).IsImpassable
                        && !visited.Contains(edgeStateId)
                    )
            )
            {
                queue.Enqueue((edge, distance + 1));
            }
        }
        _pathCache[(start, end)] = -1;
        return -1;
    }

    private void ApplyValueDistribution(IReadOnlyCollection<CountryMap> countries)
    {
        double[] targetValues = GenerateNormalDistribution(countries.Count).Order().ToArray();

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

            foreach (int stateId in country.StatesId)
            {
                var state = _stateInfoManager.GetStateInfo(stateId);
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
