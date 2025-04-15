namespace RLMod.Core.Infrastructure.Parser;

public readonly struct Rgb(byte r, byte g, byte b) : IEquatable<Rgb>
{
    private byte R { get; } = r;
    private byte G { get; } = g;
    private byte B { get; } = b;

    public static bool operator ==(Rgb a, Rgb b) => a.R == b.R && a.G == b.G && a.B == b.B;

    public static bool operator !=(Rgb a, Rgb b) => !(a == b);

    public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;

    public override readonly bool Equals(object? obj) => obj is Rgb other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(R, G, B);
}
