namespace RLMod.Core.Models.Map;

public readonly struct VictoryPoint(int provinceId, int value)
{
    public int ProvinceId { get; } = provinceId;
    public int Value { get; } = value;
}
