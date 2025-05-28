namespace RLMod.Core.Helpers;

public static class MathHelper
{
    public static int ClampValue(int value, int min = 0, int max = int.MaxValue) =>
        Math.Max(min, Math.Min(value, max));
}