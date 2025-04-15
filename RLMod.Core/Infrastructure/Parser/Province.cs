namespace RLMod.Core.Infrastructure.Parser;

public class Province
{
    public int Id { get; set; }
    public Rgb Color { get; set; }
    public string? ProvinceType { get; set; }
    public bool? IsCoastal { get; set; }
    public string? Terrain { get; set; }
    public int? ContinentId { get; set; }
    public HashSet<int> Adjacencies { get; set; } = [];
}