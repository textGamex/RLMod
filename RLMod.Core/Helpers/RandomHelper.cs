using MathNet.Numerics.Random;
using Microsoft.Extensions.DependencyInjection;
using RLMod.Core.Services;

namespace RLMod.Core.Helpers;

public static class RandomHelper
{
    public static MersenneTwister GetRandomWithSeed(bool threadSafe = false)
    {
        var settingService = App.Current.Services.GetRequiredService<AppSettingService>();
        return settingService.RandomSeed.HasValue
            ? new MersenneTwister(settingService.RandomSeed.Value, threadSafe)
            : new MersenneTwister(threadSafe);
    }
}
