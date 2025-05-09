using ParadoxPower.CSharpExtensions;
using ParadoxPower.Process;
using RLMod.Core.Extensions;
using ZLinq;

namespace RLMod.Core.Models.Map;

public sealed class StateBuildings
{
    public bool IsEmpty => _buildings.Count == 0 && _buildingsByProvince.Count == 0;

    private readonly List<Building> _buildings = [];

    /// <summary>
    /// Key 为 Province Id, Value为建筑列表
    /// </summary>
    private readonly Dictionary<int, List<Building>> _buildingsByProvince = [];

    public void Add(string name, int level)
    {
        if (level <= 0)
        {
            return;
        }

        _buildings.Add(new Building(name, level));
    }

    public int GetLevel(string name)
    {
        return _buildings.AsValueEnumerable().FirstOrDefault(building => building.Name == name)?.Level ?? 0;
    }

    public void SetLevel(string name, int level)
    {
        int index = _buildings.FindIndex(building => building.Name == name);
        if (index == -1)
        {
            return;
        }

        if (level <= 0)
        {
            _buildings.RemoveFastAt(index);
        }
        else
        {
            _buildings[index] = new Building(name, level);
        }
    }

    public void AddProvinceBuilding(int provinceId, string name, int level)
    {
        if (level <= 0)
        {
            return;
        }

        if (_buildingsByProvince.TryGetValue(provinceId, out var provinceBuildings))
        {
            provinceBuildings.Add(new Building(name, level));
        }
        else
        {
            _buildingsByProvince.Add(provinceId, [new Building(name, level)]);
        }
    }

    public Node ToNode()
    {
        var children = new List<Child>(_buildings.Count + _buildingsByProvince.Count);
        var buildingsNode = new Node("buildings");

        foreach (var building in _buildings)
        {
            children.Add(GetChildOfBuilding(building));
        }

        foreach ((int provinceId, var buildings) in _buildingsByProvince)
        {
            children.Add(ChildHelper.Node(provinceId.ToString(), buildings.Select(GetChildOfBuilding)));
        }

        buildingsNode.AllArray = children.ToArray();
        return buildingsNode;
    }

    private static Child GetChildOfBuilding(Building building)
    {
        return ChildHelper.Leaf(building.Name, building.Level);
    }
}
