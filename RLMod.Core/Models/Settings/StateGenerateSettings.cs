using System.Text.Json.Serialization;

namespace RLMod.Core.Models.Settings;

public sealed class StateGenerateSettings
{
    public int MaxFactoryNumber
    {
        get => _maxFactoryNumber;
        set => SetProperty(ref _maxFactoryNumber, value);
    }
    private int _maxFactoryNumber = 12;

    public int MaxResourceNumber
    {
        get => _maxResourceNumber;
        set => SetProperty(ref _maxResourceNumber, value);
    }
    private int _maxResourceNumber = 350;

    public double MaxFactoryNumberWeight
    {
        get => _maxFactoryNumberWeight;
        set => SetProperty(ref _maxFactoryNumberWeight, value);
    }
    private double _maxFactoryNumberWeight = 0.15;

    public double ResourcesWeight
    {
        get => _resourcesWeight;
        set => SetProperty(ref _resourcesWeight, value);
    }
    private double _resourcesWeight = 0.25;

    public double FactoryNumberWeight
    {
        get => _factoryNumberWeight;
        set => SetProperty(ref _factoryNumberWeight, value);
    }
    private double _factoryNumberWeight = 0.4;

    public int MaxVictoryPoint
    {
        get => _maxVictoryPoint;
        set => SetProperty(ref _maxVictoryPoint, value);
    }
    private int _maxVictoryPoint = 50;

    public double VictoryPointWeight
    {
        get => _victoryPointWeight;
        set => SetProperty(ref _victoryPointWeight, value);
    }
    private double _victoryPointWeight = 0.12;

    public double ManpowerWeight
    {
        get => _manpowerWeight;
        set => SetProperty(ref _manpowerWeight, value);
    }
    private double _manpowerWeight = 0.08;

    [JsonIgnore]
    public bool IsChanged { get; set; }

    [JsonConstructor]
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
