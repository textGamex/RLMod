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
        IReadOnlyCollection<CountryMap> countries,
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
        IEnumerable<CountryMap> countries,
        IReadOnlyDictionary<int, StateInfo> stateMap
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
        return countries.AsValueEnumerable().GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Count());
    }
}
