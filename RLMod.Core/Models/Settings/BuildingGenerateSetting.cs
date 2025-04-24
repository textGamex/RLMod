namespace RLMod.Core.Models.Settings;

public sealed class BuildingGenerateSetting
{
    /// <summary>
    /// 配置的建筑名称, 不存在时配置信息无效
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 当为 true 时，表示该建筑将在每个 State 中生成, 默认为<c>false</c>
    /// </summary>
    public bool IsNecessary { get; set; }
    public int? MinLevel { get; set; }
    public int? MaxLevel { get; set; }
    public double Mean { get; set; }
    public double StandardDeviation { get; set; }
    /// <summary>
    /// 该建筑在每个 State 中所占的比例
    /// </summary>
    public double Proportion { get; set; }
}
