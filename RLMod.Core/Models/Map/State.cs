using System.Diagnostics;
using MethodTimer;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Parser;
using ParadoxPower.Process;

namespace RLMod.Core.Models.Map;

public sealed class State
{
    public ushort Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public int Manpower { get; set; }
    public string Category { get; set; } = string.Empty;
    public int[] Provinces { get; set; } = [];
    public VictoryPoint[] VictoryPoints { get; set; } = [];
    public StateBuildings Buildings { get; } = new();

    [Time]
    public string ToScript()
    {
        Debug.Assert(Provinces.Length != 0);

        var state = new Node("state");
        var history = new Node("history");
        Child[] child =
        [
            ChildHelper.Leaf("id", Id),
            ChildHelper.LeafQString("name", Name),
            ChildHelper.Leaf("manpower", Manpower),
            ChildHelper.LeafString("state_category", Category),
            Child.Create(history),
            ChildHelper.Node("provinces", Provinces.Select(ChildHelper.LeafValue))
        ];

        var historyChild = new List<Child>(2 + VictoryPoints.Length)
        {
            ChildHelper.LeafString("owner", Owner)
        };

        if (!Buildings.IsEmpty)
        {
            historyChild.Add(Child.Create(Buildings.ToNode()));
        }

        historyChild.AddRange(
            VictoryPoints.Select(point =>
                ChildHelper.Node(
                    "victory_points",
                    [ChildHelper.LeafValue(point.ProvinceId), ChildHelper.LeafValue(point.Value)]
                )
            )
        );

        history.AllArray = historyChild.ToArray();
        state.AllArray = child;

        return CKPrinter.PrettyPrintStatement(state.ToRaw);
    }
}
