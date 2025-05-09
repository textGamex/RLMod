namespace RLMod.Core.Models.Map;

public sealed class State
{
    public ushort Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsImpassable { get; set; }
    public int Manpower { get; set; }
    public int[] Provinces { get; set; } = [];
    public VictoryPoint[] VictoryPoints { get; set; } = [];
}
