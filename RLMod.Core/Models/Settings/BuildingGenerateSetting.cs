using System.Text.Json.Serialization;

namespace RLMod.Core.Models.Settings;

public sealed class BuildingGenerateSetting
{
    /// <summary>
    /// 配置的建筑名称, 不存在时配置信息无效
    /// </summary>
    public string Name { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BuildingGenerateType Type { get; set; } = BuildingGenerateType.None;
    public int? MinLevel { get; set; }
    public int? MaxLevel { get; set; }
    public double Mean { get; set; }
    public double StandardDeviation { get; set; }

    /// <summary>
    /// 该建筑在每个 State 中所占的比例, 当 <see cref="IsNecessary"/> 为<c>true</c>时不生效
    /// </summary>
    public double Proportion { get; set; }

    /// <summary>
    /// 为<c>true</c>时表示此建筑只能生成在海边
    /// </summary>
    public bool NeedCoastal { get; set; }
    public BuildingReplaceSetting? ReplaceSetting { get; set; }
}
