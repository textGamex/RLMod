namespace RLMod.Core.Infrastructure.Parser;

public readonly struct Rgb(byte r, byte g, byte b) : IEquatable<Rgb>
{
    private readonly byte _r = r;
    private readonly byte _g = g;
    private readonly byte _b = b;

    public static bool operator ==(Rgb a, Rgb b) => a._r == b._r && a._g == b._g && a._b == b._b;

    public static bool operator !=(Rgb a, Rgb b) => !(a == b);

    public bool Equals(Rgb other) => _r == other._r && _g == other._g && _b == other._b;

    public override bool Equals(object? obj) => obj is Rgb other && Equals(other);

    public override int GetHashCode() => _r * 31 + _g * 31 + _b * 31;
}
