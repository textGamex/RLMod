namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateProperty
{
    public StateType Type { get; }
    public int Factories { get; set; }
    public int MaxFactories { get; }
    public int Resources { get; set; }
    public int VictoryPoint { get; }
    public StateCategory Category;

    private StateCategory RandomCategories(Random rand, int min, int max)
    {
        var categories = new List<StateCategory>();
        for (var i = min; i <= max; i++)
        {
            if (Enum.IsDefined(typeof(StateCategory), i))
                categories.Add((StateCategory)i);
        }

        var categoriesArray = categories.ToArray();
        rand.Shuffle(categoriesArray);
        categories = new List<StateCategory>(categoriesArray);
        return categories.First();
    }

    public StateProperty(TmpState state, StateType type, int maxFactoriesLimit, int resourcesLimit)
    {
        var random = Random.Shared;
        Type = type;
        VictoryPoint = state.VictoryPoint;
        switch (Type)
        {
            case StateType.Industrial:
                MaxFactories = (int)RandomCategories(
                    random,
                    (int)(0.70 * maxFactoriesLimit),
                    (int)(1.0 * maxFactoriesLimit)
                );
                break;
            case StateType.Resource:
                MaxFactories = (int)RandomCategories(
                    random,
                    (int)(0.1 * maxFactoriesLimit),
                    (int)(0.3 * maxFactoriesLimit)
                );
                break;
            case StateType.Balanced:
            default:
                MaxFactories = (int)RandomCategories(
                    random,
                    (int)(0.3 * maxFactoriesLimit),
                    (int)(0.7 * maxFactoriesLimit)
                );
                break;
        }
        switch (type)
        {
            case StateType.Industrial:
                GenerateIndustrialProperties(random, MaxFactories, resourcesLimit);
                break;
            case StateType.Resource:
                GenerateResourceProperties(random, MaxFactories, resourcesLimit);
                break;
            case StateType.Balanced:
            default:
                GenerateBalancedProperties(random, MaxFactories, resourcesLimit);
                break;
        }
    }

    private void GenerateIndustrialProperties(Random rand, int maxFactories, int maxResources)
    {
        Factories = rand.Next((int)(maxFactories * 0.5), (int)(maxFactories * 0.7));

        var resourceMax = rand.Next((int)(Factories * 10), (int)(maxResources * 0.3));
        Resources = rand.Next(0, resourceMax + 1);
    }

    private void GenerateResourceProperties(Random rand, int maxFactories, int maxResources)
    {
        Resources = rand.Next((int)(maxResources * 0.7), maxResources + 1);

        var factoryMax = Math.Min((int)(Resources * 0.005), (int)(maxFactories * 0.7));
        Factories = rand.Next(0, factoryMax + 1);
    }

    private void GenerateBalancedProperties(Random rand, int maxFactories, int maxResources)
    {
        Factories = rand.Next((int)(maxFactories * 0.3), (int)(maxFactories * 0.7) + 1);
        Resources = rand.Next((int)(maxResources * 0.3), (int)(maxResources * 0.7) + 1);

        var resourceStandard = Factories * 50.0;
        if (Math.Abs(Resources - resourceStandard) > 50)
        {
            Resources = (int)(resourceStandard + rand.Next(-25, 26));
            Resources = Math.Max(0, Math.Min(maxResources, Resources));
        }
    }

    public double Value =>
        (double)Factories
            / StatePropertyLimit.MaxMaxFactories
            * 100
            * StatePropertyLimit.FactoriesWeight
        + (double)MaxFactories
            / StatePropertyLimit.MaxMaxFactories
            * 100
            * StatePropertyLimit.MaxFactoriesWeight
        + (double)Resources
            / StatePropertyLimit.MaxResources
            * 100
            * StatePropertyLimit.ResourcesWeight
        + (double)VictoryPoint
            / StatePropertyLimit.MaxVictoryPoint
            * 100
            * StatePropertyLimit.VictoryPointWeight;
}
