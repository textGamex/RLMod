using RLMod.Core.Infrastructure.Generator;

namespace RLMod.Core.Extensions;

public static class EnumExtensions
{
    public static bool EqualsForType(this CountryType countryType, StateType stateType)
    {
        return (countryType == CountryType.Balanced && stateType == StateType.Balanced)
            || (countryType == CountryType.Resource && stateType == StateType.Resource)
            || (countryType == CountryType.Industrial && stateType == StateType.Industrial);
    }
}
