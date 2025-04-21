using ZLinq;

namespace RLMod.Core.Infrastructure.Generator;

public static class Validator
{
    public class ValidatorResult
    {
        public bool IsConnected { get; set; }
        public double ValueStdDev { get; set; }
        public Dictionary<CountryType, int> CountryTypeDistribution { get; set; } = [];
    }

    public static ValidatorResult Validate(
        IReadOnlyCollection<CountryInfo> countries,
        IReadOnlyDictionary<int, StateInfo> stateMap
    )
    {
        var result = new ValidatorResult
        {
            IsConnected = CheckConnectivity(countries, stateMap),
            ValueStdDev = CalculateValueStdDev(countries),
            CountryTypeDistribution = GetTypeDistribution(countries),
        };
        return result;
    }

    private static bool CheckConnectivity(
        IEnumerable<CountryInfo> countries,
        IReadOnlyDictionary<int, StateInfo> stateMap
    )
    {
        foreach (var country in countries)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(country.InitialId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (
                    var edgeState in stateMap[current]
                        .Edges.AsValueEnumerable()
                        .Where(state => country.ContainsState(state) || state.IsImpassable)
                )
                {
                    if (!visited.Contains(edgeState.Id))
                    {
                        queue.Enqueue(edgeState.Id);
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

    private static double CalculateValueStdDev(IEnumerable<CountryInfo> countries)
    {
        double[] values = countries.AsValueEnumerable().Select(c => c.GetValue()).ToArray();
        double mean = values.Average();
        return Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
    }

    private static Dictionary<CountryType, int> GetTypeDistribution(IEnumerable<CountryInfo> countries)
    {
        return countries.AsValueEnumerable().GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
    }
}
