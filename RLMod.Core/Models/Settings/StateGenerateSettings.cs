using System.Diagnostics;
using MemoryPack;
using RLMod.Core.Services;

namespace RLMod.Core.Models.Settings;

[MemoryPackable]
public sealed partial class StateGenerateSettings
{
    [MemoryPackOrder(0)]
    public int MaxFactoryNumber
    {
        get;
        set => SetProperty(ref field, value);
    } = 12;

    [MemoryPackOrder(1)]
    public int MaxResourceNumber
    {
        get;
        set => SetProperty(ref field, value);
    } = 600;

    [MemoryPackOrder(2)]
    public double MaxFactoryNumberWeight
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.15;

    [MemoryPackOrder(3)]
    public double ResourcesWeight
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.25;
    
    [MemoryPackOrder(4)]
    public double FactoryNumberWeight
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.4;
    
    [MemoryPackOrder(5)]
    public int MaxVictoryPoint
    {
        get;
        set => SetProperty(ref field, value);
    } = 50;

    [MemoryPackOrder(6)]
    public double VictoryPointWeight
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.2;

    [MemoryPackIgnore]
    public AppSettingService SettingService { get; init; } = null!;

    private void SetProperty<T>(ref T field, T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return;
        }
        field = newValue;
        Debug.Assert(SettingService is not null);
        SettingService.IsChanged = true;
    }
}
