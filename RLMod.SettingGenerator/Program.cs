using System.Text.Json;
using RLMod.Core.Models.Settings;

var buildings = new BuildingGenerateSetting[]
{
    new()
    {
        Name = "infrastructure",
        Type = BuildingGenerateType.Necessary,
        MinLevel = 1,
        Mean = 3,
        StandardDeviation = 1
    },
    new()
    {
        Name = "arms_factory",
        Proportion = 0.3,
        Type = BuildingGenerateType.Proportion
    },
    new()
    {
        Name = "industrial_complex",
        Proportion = 0.7,
        Type = BuildingGenerateType.Proportion
    },
    new()
    {
        Name = "dockyard",
        ReplaceSetting = new BuildingReplaceSetting { ReplaceName = "arms_factory", Proportion = 0.25 },
        NeedCoastal = true,
        Type = BuildingGenerateType.Replace
    }
};

Console.WriteLine(JsonSerializer.Serialize(buildings, new JsonSerializerOptions { WriteIndented = true }));
