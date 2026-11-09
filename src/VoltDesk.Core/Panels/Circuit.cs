using VoltDesk.Core.Common;

namespace VoltDesk.Core.Panels;

/// <summary>
/// A branch circuit in a panelboard. <see cref="Slot"/> is its circuit number; a multi-pole breaker
/// also takes the slots two and four positions below on the same side (e.g. 1, 3, 5).
/// </summary>
public sealed class Circuit : Entity
{
    public const int DescriptionMaxLength = 100;
    public const decimal MaxLoadVa = 1_000_000m;

    /// <summary>Standard ampere ratings of fuses and inverse-time breakers, NEC 240.6(A).</summary>
    public static readonly IReadOnlyList<int> StandardBreakerAmps =
    [
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100, 110, 125, 150, 175, 200,
        225, 250, 300, 350, 400, 450, 500, 600, 700, 800,
    ];

    private Circuit()
    {
    }

    public int PanelId { get; private set; }

    public int Slot { get; internal set; }

    public string Description { get; private set; } = null!;

    public LoadType LoadType { get; private set; }

    /// <summary>Connected load in volt-amperes, split evenly across the breaker's poles.</summary>
    public decimal LoadVa { get; private set; }

    public int Poles { get; private set; }

    public int BreakerAmps { get; private set; }

    public IReadOnlyList<int> Slots => SlotsFrom(Slot, Poles);

    /// <summary>Circuit number as printed on a schedule: "7" or "1,3,5".</summary>
    public string Label => string.Join(',', Slots);

    internal static IReadOnlyList<int> SlotsFrom(int slot, int poles) => [.. Enumerable.Range(0, poles).Select(i => slot + (2 * i))];

    internal static Circuit Create(int slot, string description, LoadType loadType, decimal loadVa, int poles, int breakerAmps)
    {
        if (poles is < 1 or > 3)
        {
            throw new DomainException("A breaker has 1, 2 or 3 poles.");
        }

        var circuit = new Circuit { Slot = slot, Poles = poles };
        circuit.Update(description, loadType, loadVa, breakerAmps);
        return circuit;
    }

    internal void Update(string description, LoadType loadType, decimal loadVa, int breakerAmps)
    {
        if (loadVa is < 0 or > MaxLoadVa)
        {
            throw new DomainException($"Load must be between 0 and {MaxLoadVa:#,0} VA.");
        }

        if (!StandardBreakerAmps.Contains(breakerAmps))
        {
            throw new DomainException($"{breakerAmps} A is not a standard breaker rating (NEC 240.6).");
        }

        Description = Require.Text(description, "Circuit description", DescriptionMaxLength);
        LoadType = Require.Defined(loadType, "Load type");
        LoadVa = loadVa;
        BreakerAmps = breakerAmps;
    }
}
