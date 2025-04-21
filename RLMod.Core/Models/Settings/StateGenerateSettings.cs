using MemoryPack;

namespace RLMod.Core.Models.Settings;

[MemoryPackable]
public sealed partial class StateGenerateSettings
{
    [MemoryPackOrder(0)]
    public int MaxFactoryNumber
    {
        get => _maxFactoryNumber;
        set => SetProperty(ref _maxFactoryNumber, value);
    }
    private int _maxFactoryNumber = 12;

    [MemoryPackOrder(1)]
    public int MaxResourceNumber
    {
        get => _maxResourceNumber;
        set => SetProperty(ref _maxResourceNumber, value);
    }
    private int _maxResourceNumber = 600;

    [MemoryPackOrder(2)]
    public double MaxFactoryNumberWeight
    {
        get => _maxFactoryNumberWeight;
        set => SetProperty(ref _maxFactoryNumberWeight, value);
    }
    private double _maxFactoryNumberWeight = 0.15;

    [MemoryPackOrder(3)]
    public double ResourcesWeight
    {
        get => _resourcesWeight;
        set => SetProperty(ref _resourcesWeight, value);
    }
    private double _resourcesWeight = 0.25;

    [MemoryPackOrder(4)]
    public double FactoryNumberWeight
    {
        get => _factoryNumberWeight;
        set => SetProperty(ref _factoryNumberWeight, value);
    }
    private double _factoryNumberWeight = 0.4;

    [MemoryPackOrder(5)]
    public int MaxVictoryPoint
    {
        get => _maxVictoryPoint;
        set => SetProperty(ref _maxVictoryPoint, value);
    }
    private int _maxVictoryPoint = 50;

    [MemoryPackOrder(6)]
    public double VictoryPointWeight
    {
        get => _victoryPointWeight;
        set => SetProperty(ref _victoryPointWeight, value);
    }
    private double _victoryPointWeight = 0.2;

    [MemoryPackIgnore]
    public bool IsChanged { get; set; }

    [MemoryPackConstructor]
    private StateGenerateSettings(
        int maxFactoryNumber,
        int maxResourceNumber,
        double maxFactoryNumberWeight,
        double resourcesWeight,
        double factoryNumberWeight,
        int maxVictoryPoint,
        double victoryPointWeight
    )
    {
        _maxFactoryNumber = maxFactoryNumber;
        _maxResourceNumber = maxResourceNumber;
        _maxFactoryNumberWeight = maxFactoryNumberWeight;
        _resourcesWeight = resourcesWeight;
        _factoryNumberWeight = factoryNumberWeight;
        _maxVictoryPoint = maxVictoryPoint;
        _victoryPointWeight = victoryPointWeight;
    }

    public StateGenerateSettings() { }

    private void SetProperty<T>(ref T field, T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return;
        }
        field = newValue;
        IsChanged = true;
    }
}
