namespace VoltDesk.Core.Panels;

/// <summary>Three-phase, four-wire distribution systems used in US commercial buildings.</summary>
public enum VoltageSystem
{
    /// <summary>208Y/120 V: lighting, receptacles, small equipment.</summary>
    ThreePhase208Y120,

    /// <summary>480Y/277 V: large mechanical loads and 277 V lighting.</summary>
    ThreePhase480Y277,
}

public enum Phase
{
    A,
    B,
    C,
}

public enum LoadType
{
    Lighting,
    Receptacles,
    Mechanical,
    Equipment,
    Spare,
}

public static class VoltageSystemExtensions
{
    public static decimal LineToNeutralVolts(this VoltageSystem system) => system switch
    {
        VoltageSystem.ThreePhase208Y120 => 120m,
        VoltageSystem.ThreePhase480Y277 => 277m,
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, null),
    };

    public static decimal LineToLineVolts(this VoltageSystem system) => system switch
    {
        VoltageSystem.ThreePhase208Y120 => 208m,
        VoltageSystem.ThreePhase480Y277 => 480m,
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, null),
    };

    public static string Label(this VoltageSystem system) => system switch
    {
        VoltageSystem.ThreePhase208Y120 => "208Y/120 V, 3Ø 4W",
        VoltageSystem.ThreePhase480Y277 => "480Y/277 V, 3Ø 4W",
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, null),
    };
}
