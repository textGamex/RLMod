using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using RLMod.Core.Extensions;
using ZLinq;
using ZLinq.Linq;

namespace RLMod.Core.Infrastructure.Generator;

public class TmpState
{
    public int Id;
    public bool IsImpassable;
    public int VictoryPoint;

    public HashSet<int> Adjacencies { get; set; } = [];
}

public sealed class MapGenerator
{
    private readonly Dictionary<int, StateMap> _stateMap = [];
    private readonly int _countriesCount;
    private readonly Random _random;
    private readonly double _valueMean;
    private readonly double _valueStdDev;

    public MapGenerator(
        IEnumerable<TmpState> states,
        int countriesCount = MapSettings.MaxCountry,
        int randomSeed = MapSettings.RandomSeed,
        double valueMean = 5000,
        double valueStdDev = 1000
    )
    {
        _random = new MersenneTwister(randomSeed);
        foreach (var state in states)
        {
            var type = (StateType)_random.Next(0, 3);
            _stateMap[state.Id] = new StateMap(state, type);
        }
        _countriesCount = countriesCount;
        _valueMean = valueMean;
        _valueStdDev = valueStdDev;
        CountryMap.SetStateMaps(_stateMap);
        ValidateStateCount();
    }

    private void ValidateStateCount()
    {
        int validStates = _stateMap.Count(s => !s.Value.IsImpassable);
        if (validStates < _countriesCount)
        {
            throw new ArgumentException(
                $"无法生成 {_countriesCount} 个国家，非海洋省份只有 {validStates} 个。请确保：(非海洋省份数量) ≥ (目标国家数量)"
            );
        }
    }

    public IReadOnlyCollection<CountryMap> Divide()
    {
        var countries = SelectSeeds().Select(n => new CountryMap(n)).ToList();
        bool isChange;
        do
        {
            isChange = false;

            foreach (var country in countries)
            {
                var passableBorder = country.GetPassableBorder();
                if (passableBorder.Count <= 0)
                {
                    continue;
                }

                ExpandCountry(country, passableBorder, countries);
                isChange = true;
            }
        } while (isChange);

        ApplyValueDistribution(countries);
        return countries;
    }

    private void ExpandCountry(
        CountryMap country,
        IReadOnlyCollection<int> candidates,
        List<CountryMap> countries
    )
    {
        int selected = SelectState(candidates, countries);
        country.AddState(selected);
    }

    private ValueEnumerable<
        Select<OrderBySkipTake<Where<FromEnumerable<StateMap>, StateMap>, StateMap, int>, StateMap, int>,
        int
    > SelectSeeds()
    {
        return _stateMap
            .Values.AsValueEnumerable()
            .Where(s => !s.IsImpassable)
            .OrderBy(_ => _random.Next())
            .Take(_countriesCount)
            .Select(s => s.Id);
    }

    private int SelectState(IReadOnlyCollection<int> candidates, List<CountryMap> countries)
    {
        var scores = candidates
            .AsValueEnumerable()
            .Select(id => new
            {
                Id = id,
                Value = _stateMap[id].GetValue(),
                Dispersion = CalculateDispersion(id, countries),
                TypeMatch = CalculateTypeMatch(id, countries),
            })
            .ToArray();

        var maxValues = new
        {
            Value = scores.Max(s => s.Value),
            Dispersion = scores.Max(s => s.Dispersion),
            TypeMatch = scores.Max(s => s.TypeMatch)
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

    private double CalculateDispersion(int id, List<CountryMap> countries)
    {
        double sumDistance = countries.Where(c => c.Id != id).Sum(c => ShortestPathLengthBfs(id, c.Id));

        return sumDistance / (countries.Count - 1);
    }

    private double CalculateTypeMatch(int id, List<CountryMap> countries)
    {
        var targetType = _stateMap[id].StateType;
        return countries
            .AsValueEnumerable()
            .Where(c => c.GetPassableBorder().Contains(id))
            .Average(countryMap => countryMap.Type.EqualsForType(targetType) ? 1 : 0);
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
                    .Edges.AsValueEnumerable()
                    .Where(edge => !_stateMap[edge].IsImpassable && !visited.Contains(edge))
            )
            {
                queue.Enqueue((edge, distance + 1));
            }
        }
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
            if (double.IsNaN(ratio) || ratio >= 0.95 && ratio <= 1.05)
            {
                continue;
            }

            foreach (int stateId in country.StatesId)
            {
                var state = _stateMap[stateId];
                if (state.IsImpassable)
                {
                    continue;
                }

                AdjustStateProperties(ref state, ratio);
            }
        }
    }

    private void AdjustStateProperties(ref StateMap state, double ratio)
    {
        var type = state.StateType;

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
            double factoryRatio = ratio * (0.8 + _random.NextDouble() * 0.4);
            double resourceRatio = ratio * (0.8 + _random.NextDouble() * 0.4);

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

    private static int ClampValue(int value, int max) => Math.Max(0, Math.Min(value, max));

    private double[] GenerateNormalDistribution(int count)
    {
        var normal = Normal.WithMeanStdDev(_valueMean, _valueStdDev, _random);
        double[] values = new double[count];
        normal.Samples(values);
        return values;
    }

    public static class Validator
    {
        public class ValidatorResult
        {
            public bool IsConnected { get; set; }
            public double ValueStdDev { get; set; }
            public Dictionary<CountryType, int> CountryTypeDistribution { get; set; } = [];
        }

        public static ValidatorResult Validate(
            IReadOnlyCollection<CountryMap> countries,
            Dictionary<int, StateMap> stateMap
        )
        {
            return new ValidatorResult
            {
                IsConnected = CheckConnectivity(countries, stateMap),
                ValueStdDev = CalculateValueStdDev(countries),
                CountryTypeDistribution = GetTypeDistribution(countries),
            };
        }

        private static bool CheckConnectivity(
            IEnumerable<CountryMap> countries,
            Dictionary<int, StateMap> stateMap
        )
        {
            foreach (var country in countries)
            {
                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                queue.Enqueue(country.Id);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    if (!visited.Add(current))
                    {
                        continue;
                    }

                    foreach (
                        int edge in stateMap[current]
                            .Edges.AsValueEnumerable()
                            .Where(edge => country.ContainsState(edge) || stateMap[edge].IsImpassable)
                    )
                    {
                        if (!visited.Contains(edge))
                        {
                            queue.Enqueue(edge);
                        }
                    }
                }

                if (visited.Count != country.StateCount)
                {
                    return false;
                }
            }
            return true;
        }

        private static double CalculateValueStdDev(IEnumerable<CountryMap> countries)
        {
            double[] values = countries.AsValueEnumerable().Select(c => c.GetValue()).ToArray();
            double mean = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
        }

        private static Dictionary<CountryType, int> GetTypeDistribution(IEnumerable<CountryMap> countries)
        {
            return countries
                .AsValueEnumerable()
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.Count());
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
                {
                    adj.Add(neighbor);
                }
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

            var result = Validator.Validate(countries, CountryMap.StateMaps);

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
