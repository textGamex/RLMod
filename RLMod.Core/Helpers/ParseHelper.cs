using ParadoxPower.Process;
using RLMod.Core.Infrastructure.Parser;

namespace RLMod.Core.Helpers;

public static class ParseHelper
{
    public static IEnumerable<Node> ParseAllFileToNodes(string folderPath, ParseFileType pattern)
    {
        if (!Directory.Exists(folderPath))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(folderPath, pattern.Value))
        {
            if (TextParser.TryParse(file, out var rootNode, out _))
            {
                yield return rootNode;
            }
        }
    }
}
