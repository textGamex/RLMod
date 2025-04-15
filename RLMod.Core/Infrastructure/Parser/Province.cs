namespace RLMod.Core.Infrastructure.Parser;

public sealed class Province
{
    public int Id { get; set; }
    public Rgb Color { get; set; }
    public string ProvinceType { get; set; } = string.Empty;
    public bool IsCoastal { get; set; }
    public string Terrain { get; set; } = string.Empty;
    public int ContinentId { get; set; }
    public HashSet<int> Adjacencies { get; set; } = [];
}