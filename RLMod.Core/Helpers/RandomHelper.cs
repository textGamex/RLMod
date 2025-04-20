using MathNet.Numerics.Random;
using Microsoft.Extensions.DependencyInjection;
using RLMod.Core.Services;

namespace RLMod.Core.Helpers;

public static class RandomHelper
{
    /// <summary>
    /// 获取一个随机数生成器, 如果设置了 <see cref="AppSettingService.RandomSeed"/>, 则使用该种子, 否则使用默认的随机种子.
    /// </summary>
    /// <param name="threadSafe">是否是线程安全的</param>
    /// <returns>一个随机数生成器</returns>
    public static MersenneTwister GetRandomWithSeed(bool threadSafe = false)
    {
        var settingService = App.Current.Services.GetRequiredService<AppSettingService>();
        return settingService.RandomSeed.HasValue
            ? new MersenneTwister(settingService.RandomSeed.Value, threadSafe)
            : new MersenneTwister(threadSafe);
    }
}
