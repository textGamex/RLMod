using System.Text.Json;
using RLMod.Core.Models.Settings;

var buildings = new BuildingGenerateSetting[]
{
    new()
    {
        Name = "infrastructure",
        IsNecessary = true,
        MinLevel = 1,
        Mean = 3,
        StandardDeviation = 1
    },
    new() { Name = "arms_factory", Proportion = 0.3 },
    new() { Name = "industrial_complex", Proportion = 0.7 }
};

Console.WriteLine(JsonSerializer.Serialize(buildings, new JsonSerializerOptions { WriteIndented = true }));
