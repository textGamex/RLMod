namespace RLMod.Core.Models.Settings;

public enum BuildingGenerateType : byte
{
    None,

    /// <summary>
    /// 表示该建筑将在每个 State 中生成
    /// </summary>
    Necessary,
    Proportion,
    Replace
}
