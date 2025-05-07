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

namespace RLMod.Core.Infrastructure.Generator;

public sealed class MapGenerator
{
    /// <summary>
    /// 已经被分配的省份（State）
    /// </summary>
    private static readonly HashSet<StateInfo> _occupiedStates = [];

    private readonly StateInfoManager _stateInfoManager;

    /// <summary>
    /// 目标国家（Country）数量
    /// </summary>
    private readonly int _countriesCount;
    private readonly MersenneTwister _random;
    private readonly double _valueMean;
    private readonly double _valueStdDev;

    /// <summary>
    /// Key 为起始省份（State）和结束省份（State）的哈希值，Value 为距离
    /// </summary>
    private readonly Dictionary<int, int> _pathCache = new();
    private readonly AppSettingService _settings;
    private readonly CountryTagService _countryTagService;

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// MapGenerator 构造函数，用以初始化地图生成器。
    /// </summary>
    /// <param name="states">基础省份（State）地图</param>
    /// <param name="countriesCount">目标国家数量</param>
    /// <param name="valueMean">正态分布均值（μ），默认为 5000</param>
    /// <param name="valueStdDev">正态分布标准差（σ），默认为 1000</param>
    /// <exception cref="ArgumentException">无法解析 Province 时抛出</exception>
    public MapGenerator(
        IReadOnlyList<State> states,
        int countriesCount,
        double valueMean = 5000,
        double valueStdDev = 1000
    )
    {
        _countryTagService = App.Current.Services.GetRequiredService<CountryTagService>();
        _settings = App.Current.Services.GetRequiredService<AppSettingService>();
        var provinceService = App.Current.Services.GetRequiredService<ProvinceService>();
        _countriesCount = countriesCount;
        _valueMean = valueMean;
        _valueStdDev = valueStdDev;

        if (!ProvinceParser.TryParse(_settings.GameRootFolderPath, out var provinces))
        {
            throw new ArgumentException("Could not parse province file");
        }

        _stateInfoManager = new StateInfoManager(
            states,
            provinces,
            provinceService.GetOceanProvinces(provinces)
        );
        _random = RandomHelper.GetRandomWithSeed();
        ValidateStateCountCheck();
    }

    /// <summary>
    /// 检查国家（Country）数量是否合理。
    /// </summary>
    /// <exception cref="ArgumentException">国家（Country）数量非法时抛出</exception>
    private void ValidateStateCountCheck()
    {
        int passableLandStateCount = _stateInfoManager.PassableLandStateCount;
        if (passableLandStateCount < _countriesCount)
        {
            throw new ArgumentException(
                $"无法生成 {_countriesCount} 个国家，陆地可通行省份只有 {passableLandStateCount} 个。请确保：(陆地可通行省份数量) ≥ (目标国家数量)"
            );
        }
    }

    /// <summary>
    /// 随机分配国家（Country）。
    /// </summary>
    /// <returns>国家（Country）列表</returns>
    [Time]
    public IEnumerable<CountryInfo> GenerateRandomCountries()
    {
        var countries = AssignStatesToCountries();
        Log.Debug("共生产了 {Count} 个初始国家...", countries.Length);

        ApplyValueDistribution(countries);

        // 清理资源
        _occupiedStates.Clear();
        foreach (var countryInfo in countries)
        {
            countryInfo.ClearOceanStates();
        }

        foreach (var country in countries)
        {
            country.GenerateStatesBuildingsAndResources();
        }

        AssignImpassableStates();

        return countries;
    }

    private void AssignImpassableStates()
    {
        var impassableStates = _stateInfoManager
            .States.AsValueEnumerable()
            .Where(state => state.IsImpassable);
        foreach (var state in impassableStates)
        {
            foreach (var adjacent in state.AdjacentStates)
            {
                if (adjacent is { IsPassableLand: true, Owner: not null })
                {
                    adjacent.Owner.AddState(state);
                    _occupiedStates.Add(state);
                    break;
                }
            }
        }
    }

    [Time]
    private CountryInfo[] AssignStatesToCountries()
    {
        Log.Info("计算省份（State）之间的距离分布...");
        CalculateAndCacheStatesDistance();
        Log.Info("计算省份（State）之间的距离分布完成");

        var states = GetRandomInitialState();
        var countries = GetCountryInfos(states);

        Log.Info("开始扩展...");
        AssignStatesForCountries(countries);
        Log.Info("扩展完毕");

        return countries;
    }

    private void AssignStatesForCountries(CountryInfo[] countries)
    {
        SetGrowRates(_stateInfoManager.PassableLandStateCount / _countriesCount * 2.5);
        bool isChange;
        do
        {
            isChange = false;

            foreach (var country in countries)
            {
                // 获取可通行相邻省份， 包括海洋省份
                var passableBorder = country.GetPassableBorder();

                if (country.AbleToGrow)
                {
                    isChange = TryExpandCountry(country, passableBorder, countries) || isChange;
                }
            }
        } while (isChange);
        do
        {
            isChange = false;

            foreach (var country in countries)
            {
                isChange = TryExpandCountry2(country, countries) || isChange;
            }
        } while (isChange);
    }

    private bool TryExpandCountry2(CountryInfo country, CountryInfo[] countries)
    {
        var states = _stateInfoManager.States.Where(s => !_occupiedStates.Contains(s) && !s.IsImpassable).ToArray();
        if (states.Length <= 0)
        {
            return false;
        }
        var state = states[_random.Next(states.Length)];
        if (state is null)
        {
            return false;
        }
        country.AddState(state);
        _occupiedStates.Add(state);
        return true;
    }

    /// <summary>
    /// 尝试扩展国家（Country）。
    /// </summary>
    /// <param name="country">被扩展的国家（Country）</param>
    /// <param name="candidates">候选省份（State）</param>
    /// <param name="countries">国家（country）表</param>
    /// <returns>是否扩展成功</returns>
    private bool TryExpandCountry(
        CountryInfo country,
        IReadOnlyCollection<StateInfo> candidates,
        CountryInfo[] countries
    )
    {
        var state = GetBestState(candidates, countries);
        if (state is null)
        {
            if (!country.TryGrow(_growRates, _random))
            {
                return false;
            }
            var states = _stateInfoManager.States.Where(s => !_occupiedStates.Contains(s) && !s.IsImpassable).ToArray();
            if (states.Length <= 0)
            {
                return false;
            }
            state = states[_random.Next(states.Length)];
            country.AddState(state);
            _occupiedStates.Add(state);
            return true;
        }
        if (!state.IsOcean && !country.TryGrow(_growRates, _random))
        {
            return false;
        }
        country.AddState(state);
        _occupiedStates.Add(state);
        return true;
    }

    /// <summary>
    /// 生成初始 <see cref="CountryInfo"/>, 并随机分配国家标签。
    /// </summary>
    /// <param name="states">随机初始 State 集合</param>
    /// <returns></returns>
    private CountryInfo[] GetCountryInfos(List<StateInfo> states)
    {
        var countryTags = _countryTagService.GetCountryTags().ToList();
        var countries = new CountryInfo[states.Count];
        for (int i = 0; i < states.Count; i++)
        {
            int index = _random.Next(countryTags.Count);
            string countryTag = countryTags[index];
            countryTags.RemoveFastAt(index);
            countries[i] = new CountryInfo(states[i], countryTag);
        }

        return countries;
    }

    /// <summary>
    /// 为国家（Country）选择初始省份（State）。
    /// </summary>
    /// <returns>初始省份（State）</returns>
    private List<StateInfo> GetRandomInitialState()
    {
        var selectedStates = new List<StateInfo>(_countriesCount);
        var candidates = _stateInfoManager
            .States.Where(s => s.IsPassableLand && !_occupiedStates.Contains(s))
            .ToList();
        // 第一个随机选
        var selectedState = candidates[_random.Next(candidates.Count)];
        AddState(selectedState);

        for (int i = 1; i < _countriesCount; i++)
        {
            // 剩下的在中间 15% 选
            using var sortedCandidates = candidates
                .AsValueEnumerable()
                .Select(candidate => new
                {
                    State = candidate,
                    TotalDistance = selectedStates.Sum(selected =>
                        GetStateShortestPathLength(candidate, selected.Id)
                    ),
                })
                .OrderBy(d => d.TotalDistance)
                .ToArrayPool();

            // 确定中间 15% 的范围
            int totalCandidates = sortedCandidates.Size;
            int startIndex = (int)(totalCandidates * 0.425);
            int endIndex = (int)(totalCandidates * 0.575);
            if (startIndex >= endIndex)
            {
                startIndex = 0;
                endIndex = Math.Max(0, totalCandidates - 1);
            }

            using var middleCandidates = sortedCandidates
                .AsValueEnumerable()
                .Skip(startIndex)
                .Take(endIndex - startIndex + 1)
                .Select(d => d.State)
                .ToArrayPool();

            selectedState =
                middleCandidates.Size > 0
                    ? middleCandidates.Span[_random.Next(middleCandidates.Size)]
                    : candidates[_random.Next(candidates.Count)];

            AddState(selectedState);
        }

        return selectedStates;

        void AddState(StateInfo state)
        {
            selectedStates.Add(state);
            _occupiedStates.Add(state);
            candidates.Remove(state);
        }
    }

    /// <summary>
    /// 获取最优扩展的省份（State）。
    /// </summary>
    /// <param name="candidates">候选省份（State）</param>
    /// <param name="countries">国家（Country）表</param>
    /// <returns>最优省份（State）</returns>
    private StateInfo? GetBestState(IReadOnlyCollection<StateInfo> candidates, CountryInfo[] countries)
    {
        if (candidates is null)
        {
            return null;
        }
        using var validCandidates = candidates
            .AsValueEnumerable()
            .Where(s => !_occupiedStates.Contains(s))
            .ToArrayPool();
        if (validCandidates.Size <= 0)
        {
            return null;
        }

        // 评估得分
        using var scores = validCandidates
            .AsValueEnumerable()
            .Select(stateInfo => new
            {
                State = stateInfo,
                Dispersion = GetStateDispersion(stateInfo, countries),
                TypeMatch = GetStateTypeMatch(stateInfo, countries),
            })
            .ToArrayPool();

        var maxValues = new
        {
            Value = scores.AsValueEnumerable().Max(s => s.State.Value),
            Dispersion = scores.AsValueEnumerable().Max(s => s.Dispersion),
            TypeMatch = scores.AsValueEnumerable().Max(s => s.TypeMatch),
        };

        return scores
            .AsValueEnumerable()
            .Select(stateInfo => new
            {
                StateInfo = stateInfo,
                Score = 0.5 * (stateInfo.State.Value / maxValues.Value)
                    + 0.3 * (stateInfo.Dispersion / maxValues.Dispersion)
                    + 0.2 * (stateInfo.TypeMatch / maxValues.TypeMatch),
            })
            .OrderByDescending(s => s.Score)
            .First()
            .StateInfo.State;
    }

    /// <summary>
    /// 计算省份（State）之间的距离分布
    /// </summary>
    /// <param name="state">目标省份（State）</param>
    /// <param name="countries">国家（Country）表</param>
    /// <returns></returns>
    private double GetStateDispersion(StateInfo state, CountryInfo[] countries)
    {
        // 计算其他国家非初始省份的距离和
        int sumDistance = countries
            .AsValueEnumerable()
            .Where(c => c.InitialId != state.Id)
            .Sum(c => GetStateShortestPathLength(state, c.InitialId));
        // 返回平均值
        return (double)sumDistance / (countries.Length - 1);
    }

    /// <summary>
    /// 计算省份（State）类型匹配度
    /// </summary>
    /// <param name="state">目标省份（State）</param>
    /// <param name="countries">国家（Country）表</param>
    /// <returns></returns>
    private static double GetStateTypeMatch(StateInfo state, CountryInfo[] countries)
    {
        //计算国家相邻省份类型相同平均值
        var targetType = state.Type;
        return countries
            .AsValueEnumerable()
            .Where(c => c.GetPassableBorder().Contains(state))
            .Average(countryMap => countryMap.Type.EqualsForType(targetType) ? 1 : 0);
    }

    /// <summary>
    /// 计算所有省份（State）之间的最短路径长度。
    /// </summary>
    private void CalculateAndCacheStatesDistance()
    {
        for (int i = 0; i < _stateInfoManager.States.Count; i++)
        {
            var start = _stateInfoManager.States[i];
            for (int j = i; j < _stateInfoManager.States.Count; j++)
            {
                var end = _stateInfoManager.States[j];
                GetStateShortestPathLength(start, end.Id);
            }
        }
    }

    // TODO: 拆分 GetStateShortestPathLength 方法以降低复杂度

    /// <summary>
    /// 使用 Dijkstra 算法计算两个省份（State）之间的最短路径长度，并储存起始省份单源最短路。
    /// </summary>
    /// <param name="startState">起始省份（State）</param>
    /// <param name="endStateId">结束省份（State）</param>
    /// <returns>两个省份（State）之间的最短路径长度</returns>
    private int GetStateShortestPathLength(StateInfo startState, int endStateId)
    {
        int hash = GetCacheHashCode(startState.Id, endStateId);
        if (_pathCache.TryGetValue(hash, out int cached))
        {
            return cached;
        }

        if (startState.Id == endStateId)
        {
            _pathCache[hash] = 0;
            return 0;
        }
        var visited = new HashSet<int>();
        var priorityQueue = new PriorityQueue<StateInfo, int>();
        var distances = new Dictionary<int, int> { [startState.Id] = 0 };
        priorityQueue.Enqueue(startState, 0);
        while (priorityQueue.Count > 0)
        {
            var currentState = priorityQueue.Dequeue();
            if (!visited.Add(currentState.Id))
            {
                continue;
            }

            foreach (
                var edgeState in currentState
                    .AdjacentStates.AsValueEnumerable()
                    .Where(state => !state.IsImpassable && !visited.Contains(state.Id))
            )
            {
                int newDistance = distances[currentState.Id] + 1;
                if (
                    !distances.TryGetValue(edgeState.Id, out int existingDistance)
                    || newDistance < existingDistance
                )
                {
                    distances[edgeState.Id] = newDistance;
                    priorityQueue.Enqueue(edgeState, newDistance);
                }
            }
        }
        foreach (var distance in distances)
        {
            _pathCache[GetCacheHashCode(startState.Id, distance.Key)] = distance.Value;
        }
        if (distances.TryGetValue(endStateId, out int result))
        {
            return result;
        }
        else
        {
            _pathCache[hash] = -1;
            return -1;
        }

        // 生成一个顺序无关的 HashCode
        static int GetCacheHashCode(int startId, int endId)
        {
            unchecked
            {
                if (startId > endId)
                {
                    return (startId * 31) ^ endId;
                }

                return (endId * 31) ^ startId;
            }
        }
    }

    private void ApplyValueDistribution(IReadOnlyCollection<CountryInfo> countries)
    {
        double[] targetValues = GenerateNormalDistribution(countries.Count);
        Array.Sort(targetValues);

        var orderedCountries = countries.AsValueEnumerable().OrderBy(c => c.GetValue()).ToArray();

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
                if (!state.IsPassableLand)
                {
                    continue;
                }

                AdjustStateProperties(state, ratio);
            }
        }
    }

    /// <summary>
    /// 调整属性以符合正态分布
    /// </summary>
    /// <param name="state"></param>
    /// <param name="ratio"></param>
    private void AdjustStateProperties(StateInfo state, double ratio)
    {
        const double industrialFactoryMinRatio = 0.8;
        const double industrialResourceMaxRatio = 1.0;
        const double resourceResourceMinRatio = 0.8;

        var type = state.Type;
        int originalFactories = state.FactorySum;
        int originalResources = state.ResourceSum;

        if (type == StateType.Industrial)
        {
            double factoryRatio = ratio * 1.2;
            factoryRatio = Math.Max(industrialFactoryMinRatio, Math.Min(1.2, factoryRatio));

            state.FactorySum = MathHelper.ClampValue(
                (int)(originalFactories * factoryRatio),
                min: (int)(originalFactories * industrialFactoryMinRatio),
                max: _settings.StateGenerate.MaxFactoryNumber
            );

            double resourceRatio = ratio * 0.8;
            resourceRatio = Math.Min(industrialResourceMaxRatio, resourceRatio);
            state.ResourceSum = MathHelper.ClampValue(
                (int)(originalResources * resourceRatio),
                max: _settings.StateGenerate.MaxResourceNumber
            );
        }
        else if (type == StateType.Resource)
        {
            double resourceRatio = ratio * 1.2;
            resourceRatio = Math.Max(resourceResourceMinRatio, Math.Min(1.2, resourceRatio));
            state.ResourceSum = MathHelper.ClampValue(
                (int)(originalResources * resourceRatio),
                min: (int)(originalResources * resourceResourceMinRatio),
                max: _settings.StateGenerate.MaxResourceNumber
            );

            state.FactorySum = MathHelper.ClampValue(
                (int)(originalFactories * ratio * 0.8),
                max: _settings.StateGenerate.MaxFactoryNumber
            );
        }
        else
        {
            double factoryRatio = ratio * (0.9 + _random.NextDouble() * 0.2); // 0.9-1.1
            double resourceRatio = ratio * (0.9 + _random.NextDouble() * 0.2);

            state.FactorySum = MathHelper.ClampValue(
                (int)(originalFactories * factoryRatio),
                min: (int)(originalFactories * 0.7),
                max: _settings.StateGenerate.MaxFactoryNumber
            );
            state.ResourceSum = MathHelper.ClampValue(
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

    private double[] GenerateNormalDistribution(int count)
    {
        var normal = Normal.WithMeanStdDev(_valueMean, _valueStdDev, _random);
        double[] values = new double[count];
        normal.Samples(values);
        return values;
    }

    private double[] _growRates = [];

    private void SetGrowRates(double size)
    {
        const double sigma = 1;
        double mu = size / 2;
        List<double> probs = [];

        for (int k = 1; k <= size; k++)
        {
            double cdfLow,
                cdfHigh;

            if (k == 1)
            {
                // 区间 (-∞, 1.5)
                cdfLow = 0.0; // CDF(-∞) = 0
                cdfHigh = Normal.CDF(mu, sigma, 1.5);
            }
            else if (k == size)
            {
                // 区间 [X-0.5, +∞)
                cdfLow = Normal.CDF(mu, sigma, size - 0.5);
                cdfHigh = 1.0; // CDF(+∞) = 1
            }
            else
            {
                // 区间 [k-0.5, k+0.5)
                cdfLow = Normal.CDF(mu, sigma, k - 0.5);
                cdfHigh = Normal.CDF(mu, sigma, k + 0.5);
            }

            probs.Add(cdfHigh - cdfLow);
        }

        double total = probs.Sum();
        _growRates = new double[probs.Count];
        _growRates[0] = probs[0] / total * 100;
        for (int i = 1; i < probs.Count; i++)
        {
            _growRates[i] = probs[i] / total * 100 + _growRates[i - 1];
        }
    }
}
