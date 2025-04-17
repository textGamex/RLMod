using Ardalis.SmartEnum;

namespace RLMod.Core.Helpers;

public sealed class ParseFileType : SmartEnum<ParseFileType, string>
{
    public static readonly ParseFileType Text = new(nameof(Text), "*.txt");

    private ParseFileType(string name, string value)
        : base(name, value) { }
}
