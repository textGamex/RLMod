namespace RLMod.Core.Extensions;

public static class StringExtensions
{
    public static bool EqualsIgnoreCase(this string str, string value)
    {
        return str.Equals(value, StringComparison.OrdinalIgnoreCase);
    }
}
