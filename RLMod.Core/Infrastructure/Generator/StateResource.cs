using System.Diagnostics;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Process;
using RLMod.Core.Models.Map;

namespace RLMod.Core.Infrastructure.Generator;

public sealed class StateResource
{
    public bool IsEmpty => _resources.Count == 0;

    private readonly List<Resource> _resources = [];

    public void Add(string name, int amount)
    {
        _resources.Add(new Resource(name, amount));
    }

    public Node ToNode()
    {
        var resourcesNode = new Node("resources");
        var resources = new Child[_resources.Count];

        for (int i = 0; i < _resources.Count; i++)
        {
            var resource = _resources[i];
            resources[i] = ChildHelper.Leaf(resource.Name, resource.Amount);
            Debug.Assert(resource.Amount > 0);
        }
        resourcesNode.AllArray = resources;

        return resourcesNode;
    }
}
