using RLMod.Core.Models.Map;

namespace RLMod.Core.Infrastructure.Parser;

public sealed class Province
{
    public int Id { get; set; }
    public Rgb Color { get; set; }
    public ProvinceType Type { get; init; } = ProvinceType.None;
    public bool IsCoastal { get; init; }
    public string Terrain { get; set; } = string.Empty;
    public int ContinentId { get; set; }
    public HashSet<int> Adjacencies { get; } = [];
}
