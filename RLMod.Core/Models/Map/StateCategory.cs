namespace RLMod.Core.Models.Map;

public sealed class StateCategory(string name, int slots)
{
    public string Name { get; } = name;
    public int Slots { get; } = slots;
}