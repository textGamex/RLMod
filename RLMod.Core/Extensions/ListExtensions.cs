namespace RLMod.Core.Extensions;

public static class ListExtensions
{
    public static void RemoveFastAt<T>(this List<T> list, int index)
    {
        if (index < 0 || index >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        list[index] = list[^1];
        list.RemoveAt(list.Count - 1);
    }
}
