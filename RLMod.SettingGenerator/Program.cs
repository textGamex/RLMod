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

var resources = new ResourceGenerateSetting[]
{
    new() { Name = "steel", Proportion = 0.45 },
    new() { Name = "oil", Proportion = 0.05 },
    new() { Name = "aluminium", Proportion = 0.10 },
    new() { Name = "rubber", Proportion = 0.15 },
    new() { Name = "tungsten", Proportion = 0.12 },
    new() { Name = "chromium", Proportion = 0.13 }
};
var options = new JsonSerializerOptions { WriteIndented = true };

Console.WriteLine(JsonSerializer.Serialize(buildings, options));
Console.WriteLine(new string('-', 30));
Console.WriteLine(JsonSerializer.Serialize(resources, options));
