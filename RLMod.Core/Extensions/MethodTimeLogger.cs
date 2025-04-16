using System.Reflection;
using NLog;

namespace RLMod.Core.Extensions;

public static class MethodTimeLogger
{
    private static readonly Logger Logger = LogManager.GetLogger("MethodTime");

    public static void Log(MethodBase methodBase, TimeSpan elapsed, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Logger.Debug("{Name} 耗时: {Time:F2} ms", methodBase.Name, elapsed.TotalMilliseconds);
        }
        else
        {
            Logger.Debug(
                "{Name} {Message} 耗时: {Time:F2} ms",
                methodBase.Name,
                message,
                elapsed.TotalMilliseconds
            );
        }
    }
}
