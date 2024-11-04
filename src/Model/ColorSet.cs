namespace Fireworks2D.Model;

/// <summary>
/// Holds a list of colors with a name.
/// </summary>
public class ColorSet
{
    public string Name;
    public uint[] Colors;
    
    public override string ToString() => $"ColorSet: {Name}";
}
