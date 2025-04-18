namespace RLMod.Core.Infrastructure.Generator;

public class TmpState
{
    public int Id;
    public bool IsImpassable;
    public int VictoryPoint;

    public HashSet<int> Adjacencies { get; set; } = [];
}

public enum StateType
{
    Industrial,
    Resource,
    Balanced,
}

public enum CountryType
{
    Industrial,
    Resource,
    Balanced,
}

public static class StatePropertyLimit
{
    public const int MaxMaxFactories = 25;
    public const int MaxResources = 600;
    public const int MaxVictoryPoint = 50;
    public const double FactoriesWeight = 0.4;
    public const double MaxFactoriesWeight = 0.15;
    public const double ResourcesWeight = 0.25;
    public const double VictoryPointWeight = 0.20;
}

public static class MapSettings
{
    public const int MaxCountry = 100;
    public const int Sigma = 10;
    public const int RandomSeed = 114514;
}

public struct StateProperty
{
    public StateType Type;
    public int Factories;
    public int MaxFactories;
    public int Resources;
    public int VictoryPoint;

    public StateProperty(TmpState state, StateType type, int maxFactoriesLimit, int resourcesLimit)
    {
        var rand = new Random(Guid.NewGuid().GetHashCode());
        Type = type;
        VictoryPoint = state.VictoryPoint;

        switch (type)
        {
            case StateType.Industrial:
                Factories = rand.Next((int)(maxFactoriesLimit * 0.7), maxFactoriesLimit);
                MaxFactories = maxFactoriesLimit;
                Resources = rand.Next(0, (int)(resourcesLimit * 0.3));
                break;
            case StateType.Resource:
                Factories = rand.Next(0, (int)(maxFactoriesLimit * 0.3));
                MaxFactories = (int)(maxFactoriesLimit * 0.5);
                Resources = rand.Next((int)(resourcesLimit * 0.7), resourcesLimit);
                break;
            case StateType.Balanced:
            default:
                Factories = rand.Next((int)(maxFactoriesLimit * 0.3), (int)(maxFactoriesLimit * 0.7));
                MaxFactories = (int)(maxFactoriesLimit * 0.8);
                Resources = rand.Next((int)(resourcesLimit * 0.3), (int)(resourcesLimit * 0.7));
                break;
        }
    }

    public double Value =>
        (double)Factories / StatePropertyLimit.MaxMaxFactories * 100 * StatePropertyLimit.FactoriesWeight
        + (double)MaxFactories
            / StatePropertyLimit.MaxMaxFactories
            * 100
            * StatePropertyLimit.MaxFactoriesWeight
        + (double)Resources / StatePropertyLimit.MaxResources * 100 * StatePropertyLimit.ResourcesWeight
        + (double)VictoryPoint
            / StatePropertyLimit.MaxVictoryPoint
            * 100
            * StatePropertyLimit.VictoryPointWeight;
}

public class StateMap(TmpState state, StateType type)
{
    private readonly int _id = state.Id;
    private readonly bool _isImpassable = state.IsImpassable;
    private StateProperty _stateProperties = new(
        state,
        type,
        StatePropertyLimit.MaxMaxFactories,
        StatePropertyLimit.MaxResources
    );
    public int Factories
    {
        get => _stateProperties.Factories;
        set => _stateProperties.Factories = value;
    }

    public int Resources
    {
        get => _stateProperties.Resources;
        set => _stateProperties.Resources = value;
    }
    public HashSet<int> Edges { get; } = [.. state.Adjacencies];

    public bool IsImpassable() => _isImpassable;

    public int GetId() => _id;

    public HashSet<int> GetEdges()
    {
        return Edges;
    }

    /// <summary>
    /// 计算获取省份的价值。
    /// </summary>
    /// <returns>省份的价值</returns>
    public double GetValue()
    {
        return _isImpassable ? 0 : _stateProperties.Value;
    }

    public StateType GetStateType() => _stateProperties.Type;
}

public class CountryMap
{
    private readonly int _id;

    // private string _tag = "D00";
    private HashSet<int> _states { get; } = [];
    private HashSet<int> _border { get; } = [];
    private static Dictionary<int, StateMap> _stateMaps = [];
    public CountryType Type;

    public int GetId() => _id;

    public List<int> GetStates() => _states.ToList();

    public static void SetStateMaps(Dictionary<int, StateMap> stateMaps) => _stateMaps = stateMaps;

    public static Dictionary<int, StateMap> GetStateMaps() => _stateMaps;

    public CountryMap(int seed)
    {
        _id = seed;
        AddState(seed);
    }

    /// <summary>
    /// 计算获取国家的价值。
    /// </summary>
    /// <returns>国家的价值</returns>

    public double GetValue() => _states.Sum(s => _stateMaps[s].GetValue());

    public List<int> GetPassableBorder() => _border.Where(n => !_stateMaps[n].IsImpassable()).ToList();

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
        foreach (int edge in _stateMaps[addedState].Edges.Where(edge => !_states.Contains(edge)))
        {
            _border.Add(edge);
        }

        _border.Remove(addedState);
    }

    private void UpdateCountryType()
    {
        var typeGroups = _states
            .Select(s => _stateMaps[s].GetStateType())
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

public sealed class MapGenerator
{
    private readonly Dictionary<int, StateMap> _stateMap = [];
    private readonly int _countriesCount;
    private readonly Random _rand;
    private readonly double _valueMean;
    private readonly double _valueStdDev;

    public MapGenerator(
        List<TmpState> states,
        int countriesCount = MapSettings.MaxCountry,
        int randomSeed = MapSettings.RandomSeed,
        double valueMean = 5000,
        double valueStdDev = 1000
    )
    {
        var typeRand = new Random(MapSettings.RandomSeed);
        foreach (var state in states)
        {
            var type = (StateType)typeRand.Next(0, 3);
            _stateMap[state.Id] = new StateMap(state, type);
        }
        _countriesCount = countriesCount;
        _rand = new Random(randomSeed);
        _valueMean = valueMean;
        _valueStdDev = valueStdDev;
        CountryMap.SetStateMaps(_stateMap);
        ValidateStateCount();
    }

    private void ValidateStateCount()
    {
        int validStates = _stateMap.Count(s => !s.Value.IsImpassable());
        if (validStates < _countriesCount)
        {
            throw new ArgumentException(
                $"无法生成 {_countriesCount} 个国家，非海洋省份只有 {validStates} 个。"
                    + $"请确保：(非海洋省份数量) ≥ (目标国家数量)"
            );
        }
    }

    public List<CountryMap> Divide()
    {
        var seeds = SelectSeeds();
        var countries = seeds.Select(n => new CountryMap(n)).ToList();
        bool changeFlag;
        do
        {
            changeFlag = false;
            foreach (var country in countries.Where(c => c.GetPassableBorder().Count > 0))
            {
                if (TryExpandCountry(country, countries))
                    changeFlag = true;
            }
        } while (changeFlag);

        ApplyValueDistribution(countries);
        return countries;
    }

    private bool TryExpandCountry(CountryMap country, List<CountryMap> countries)
    {
        var candidates = country.GetPassableBorder();
        if (candidates.Count == 0)
            return false;

        int selected = SelectState(candidates, countries);
        country.AddState(selected);
        return true;
    }

    private List<int> SelectSeeds()
    {
        return _stateMap
            .Values.Where(s => !s.IsImpassable())
            .OrderBy(_ => _rand.Next())
            .Take(_countriesCount)
            .Select(s => s.GetId())
            .ToList();
    }

    private int SelectState(List<int> candidates, List<CountryMap> countries)
    {
        var scores = candidates
            .Select(id => new
            {
                Id = id,
                Value = _stateMap[id].GetValue(),
                Dispersion = CalculateDispersion(id, countries),
                TypeMatch = CalculateTypeMatch(id, countries),
            })
            .ToList();

        var maxValues = new
        {
            Value = scores.Max(s => s.Value),
            Dispersion = scores.Max(s => s.Dispersion),
            TypeMatch = scores.Max(s => s.TypeMatch),
        };

        return scores
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

    private double CalculateDispersion(int id, List<CountryMap> countries)
    {
        double sumDistance = countries
            .Where(c => c.GetId() != id)
            .Sum(c => ShortestPathLengthBfs(id, c.GetId()));

        return sumDistance / (countries.Count - 1);
    }

    private double CalculateTypeMatch(int id, List<CountryMap> countries)
    {
        var targetType = _stateMap[id].GetStateType();
        return countries
            .Where(c => c.GetPassableBorder().Contains(id))
            .Average(c => c.Type.ToString() == targetType.ToString() ? 1 : 0);
    }

    private int ShortestPathLengthBfs(int start, int end)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();
            if (current == end)
            {
                return distance;
            }
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (
                int edge in _stateMap[current]
                    .GetEdges()
                    .Where(edge => !_stateMap[edge].IsImpassable() && !visited.Contains(edge))
            )
            {
                queue.Enqueue((edge, distance + 1));
            }
        }
        return -1;
    }

    private void ApplyValueDistribution(List<CountryMap> countries)
    {
        List<double> targetValues = GenerateNormalDistribution(countries.Count).OrderBy(v => v).ToList();

        var orderedCountries = countries.OrderBy(c => c.GetValue()).ToList();

        for (int i = 0; i < orderedCountries.Count; i++)
        {
            CountryMap country = orderedCountries[i];
            double currentValue = country.GetValue();
            double targetValue = targetValues[i];

            double ratio = targetValue / currentValue;
            if (double.IsNaN(ratio) || ratio >= 0.95 && ratio <= 1.05)
                continue;

            foreach (int stateId in country.GetStates())
            {
                StateMap state = _stateMap[stateId];
                if (state.IsImpassable())
                    continue;

                AdjustStateProperties(ref state, ratio);
            }
        }
    }

    private void AdjustStateProperties(ref StateMap state, double ratio)
    {
        StateType type = state.GetStateType();
        Random rand = new Random(Guid.NewGuid().GetHashCode());

        if (type == StateType.Industrial)
        {
            state.Factories = ClampValue(
                (int)(state.Factories * ratio * 1.2),
                StatePropertyLimit.MaxMaxFactories
            );
            state.Resources = ClampValue((int)(state.Resources * ratio), StatePropertyLimit.MaxResources);
        }
        else if (type == StateType.Resource)
        {
            state.Resources = ClampValue(
                (int)(state.Resources * ratio * 1.2),
                StatePropertyLimit.MaxResources
            );
            state.Factories = ClampValue((int)(state.Factories * ratio), StatePropertyLimit.MaxMaxFactories);
        }
        else
        {
            double factoryRatio = ratio * (0.8 + rand.NextDouble() * 0.4);
            double resourceRatio = ratio * (0.8 + rand.NextDouble() * 0.4);

            state.Factories = ClampValue(
                (int)(state.Factories * factoryRatio),
                StatePropertyLimit.MaxMaxFactories
            );
            state.Resources = ClampValue(
                (int)(state.Resources * resourceRatio),
                StatePropertyLimit.MaxResources
            );
        }
    }

    private int ClampValue(int value, int max) => Math.Max(0, Math.Min(value, max));

    private List<double> GenerateNormalDistribution(int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(_ => NextGaussian(_rand, _valueMean, _valueStdDev))
            .OrderBy(v => v)
            .ToList();
    }

    public static double NextGaussian(Random rand, double mean, double stdDev)
    {
        double u1 = 1.0 - rand.NextDouble();
        double u2 = 1.0 - rand.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    public static class Validator
    {
        public class ValidatorResult
        {
            public bool IsConnected { get; set; }
            public double ValueStdDev { get; set; }
            public Dictionary<CountryType, int> CountryTypeDistribution { get; set; } = [];
        }

        public static ValidatorResult Validate(List<CountryMap> countries, Dictionary<int, StateMap> stateMap)
        {
            return new ValidatorResult
            {
                IsConnected = CheckConnectivity(countries, stateMap),
                ValueStdDev = CalculateValueStdDev(countries),
                CountryTypeDistribution = GetTypeDistribution(countries),
            };
        }

        private static bool CheckConnectivity(List<CountryMap> countries, Dictionary<int, StateMap> stateMap)
        {
            foreach (var country in countries)
            {
                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                queue.Enqueue(country.GetId());

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    if (!visited.Add(current))
                        continue;

                    foreach (
                        int edge in stateMap[current]
                            .Edges.Where(e => country.ContainsState(e) || stateMap[e].IsImpassable())
                    )
                    {
                        if (!visited.Contains(edge))
                            queue.Enqueue(edge);
                    }
                }

                if (visited.Count != country.GetStateCount())
                    return false;
            }
            return true;
        }

        private static double CalculateValueStdDev(List<CountryMap> countries)
        {
            var values = countries.Select(c => c.GetValue()).ToList();
            double mean = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
        }

        private static Dictionary<CountryType, int> GetTypeDistribution(List<CountryMap> countries)
        {
            return countries.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
        }
    }

    public static class TestProgram
    {
        public static List<TmpState> GenerateTestStates(int count)
        {
            var rand = new Random(114514);
            var states = new List<TmpState>();

            for (int i = 0; i < count; i++)
            {
                states.Add(
                    new TmpState
                    {
                        Id = i,
                        IsImpassable = rand.NextDouble() < 0.1,
                        VictoryPoint = rand.Next(0, StatePropertyLimit.MaxVictoryPoint),
                        Adjacencies = GenerateAdjacencies(i, count, rand),
                    }
                );
            }
            return states;
        }

        private static HashSet<int> GenerateAdjacencies(int currentId, int maxId, Random rand)
        {
            var adj = new HashSet<int>();
            int numConnections = rand.Next(1, 5);
            for (int i = 0; i < numConnections; i++)
            {
                int neighbor = rand.Next(0, maxId);
                if (neighbor != currentId)
                    adj.Add(neighbor);
            }
            return adj;
        }

        public static void TestMain()
        {
            var testStates = GenerateTestStates(1000);

            var generator = new MapGenerator(
                states: testStates,
                countriesCount: 50,
                randomSeed: 114514,
                valueMean: 5000,
                valueStdDev: 1000
            );

            var countries = generator.Divide();

            var result = Validator.Validate(countries, CountryMap.GetStateMaps());

            Console.WriteLine($"连通性验证: {result.IsConnected}");
            Console.WriteLine($"价值标准差: {result.ValueStdDev:F2}");
            Console.WriteLine("国家类型分布:");
            foreach (var kvp in result.CountryTypeDistribution)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value} 个国家");
            }
        }
    }
}
