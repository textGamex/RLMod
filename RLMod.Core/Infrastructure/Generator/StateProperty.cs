namespace RLMod.Core.Infrastructure.Generator;

public struct StateProperty
{
    public StateType Type;
    public int Factories;
    public int MaxFactories;
    public int Resources;
    public int VictoryPoint;

    public StateProperty(TmpState state, StateType type, int maxFactoriesLimit, int resourcesLimit)
    {
        var random = Random.Shared;
        Type = type;
        VictoryPoint = state.VictoryPoint;

        switch (type)
        {
            case StateType.Industrial:
                Factories = random.Next((int)(maxFactoriesLimit * 0.7), maxFactoriesLimit);
                MaxFactories = maxFactoriesLimit;
                Resources = random.Next(0, (int)(resourcesLimit * 0.3));
                break;
            case StateType.Resource:
                Factories = random.Next(0, (int)(maxFactoriesLimit * 0.3));
                MaxFactories = (int)(maxFactoriesLimit * 0.5);
                Resources = random.Next((int)(resourcesLimit * 0.7), resourcesLimit);
                break;
            case StateType.Balanced:
            default:
                Factories = random.Next((int)(maxFactoriesLimit * 0.3), (int)(maxFactoriesLimit * 0.7));
                MaxFactories = (int)(maxFactoriesLimit * 0.8);
                Resources = random.Next((int)(resourcesLimit * 0.3), (int)(resourcesLimit * 0.7));
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
