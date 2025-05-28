using System.ComponentModel;
using Microsoft.FSharp.Collections;
using ParadoxPower.CSharpExtensions;
using ParadoxPower.Parser;
using ParadoxPower.Process;
using ParadoxPower.Utilities;

namespace RLMod.Core.Extensions;

public static class ParserExtensions
{
    public static Types.Statement GetRawStatement(this Child child, string key)
    {
        if (child.TryGetNode(out var node))
        {
            return node.ToRaw;
        }

        if (child.TryGetLeaf(out var leaf))
        {
            return leaf.ToRaw;
        }

        if (child.TryGetLeafValue(out var value))
        {
            return value.ToRaw;
        }

        if (child.TryGetComment(out var comment))
        {
            return Types.Statement.NewCommentStatement(comment);
        }

        if (child.TryGetValueClause(out var clause))
        {
            var keys = new Types.Statement[clause.Keys.Length + 1];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = Types.Statement.NewValue(
                    Position.Range.Zero,
                    Types.Value.NewString(clause.Keys[i])
                );
            }
            keys[^1] = Types.Statement.NewValue(
                clause.Position,
                Types.Value.NewClause(clause.ToRaw)
            );

            return Types.Statement.NewKeyValue(
                Types.PosKeyValue.NewPosKeyValue(
                    clause.Position,
                    Types.KeyValueItem.NewKeyValueItem(
                        Types.Key.NewKey(key),
                        Types.Value.NewClause(ListModule.OfArray(keys)),
                        Types.Operator.Equals
                    )
                )
            );
        }

        throw new InvalidEnumArgumentException(nameof(child));
    }
}
