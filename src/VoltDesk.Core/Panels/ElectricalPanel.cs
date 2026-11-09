using VoltDesk.Core.Common;

namespace VoltDesk.Core.Panels;

/// <summary>
/// A panelboard and its schedule of circuits. Slots are numbered like a real panel: odd on the
/// left, even on the right, and each row of two slots sits on phase A, B, C, A, B, C… in turn.
/// </summary>
public sealed class ElectricalPanel : Entity
{
    public const int NameMaxLength = 30;
    public const int LocationMaxLength = 100;

    public static readonly IReadOnlyList<int> StandardSpaces = [12, 18, 24, 30, 42, 54, 60, 72, 84];

    public static readonly IReadOnlyList<int> StandardBusAmps = [100, 125, 150, 200, 225, 250, 400, 600, 800, 1200];

    private readonly List<Circuit> _circuits = [];

    private ElectricalPanel()
    {
    }

    public int ProjectId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Location { get; private set; }

    public VoltageSystem System { get; private set; }

    public int BusRatingAmps { get; private set; }

    /// <summary>Main breaker rating; null for a main-lugs-only panel.</summary>
    public int? MainBreakerAmps { get; private set; }

    public int Spaces { get; private set; }

    public IReadOnlyList<Circuit> Circuits => _circuits;

    public static Phase PhaseOf(int slot) => (Phase)((slot - 1) / 2 % 3);

    public static ElectricalPanel Create(
        int projectId,
        string name,
        string? location,
        VoltageSystem system,
        int busRatingAmps,
        int? mainBreakerAmps,
        int spaces)
    {
        if (!StandardSpaces.Contains(spaces))
        {
            throw new DomainException($"A panel has {string.Join(", ", StandardSpaces)} spaces, not {spaces}.");
        }

        var panel = new ElectricalPanel
        {
            ProjectId = projectId,
            System = Require.Defined(system, "Voltage system"),
            Spaces = spaces,
        };
        panel.UpdateDetails(name, location, busRatingAmps, mainBreakerAmps);
        return panel;
    }

    public void UpdateDetails(string name, string? location, int busRatingAmps, int? mainBreakerAmps)
    {
        if (!StandardBusAmps.Contains(busRatingAmps))
        {
            throw new DomainException($"{busRatingAmps} A is not a standard panelboard bus rating.");
        }

        if (mainBreakerAmps is { } main && (!Circuit.StandardBreakerAmps.Contains(main) || main > busRatingAmps))
        {
            throw new DomainException($"The main breaker must be a standard rating no larger than the {busRatingAmps} A bus.");
        }

        Name = Require.Text(name, "Panel name", NameMaxLength).ToUpperInvariant();
        Location = Require.OptionalText(location, "Location", LocationMaxLength);
        BusRatingAmps = busRatingAmps;
        MainBreakerAmps = mainBreakerAmps;
    }

    public Circuit AddCircuit(int slot, string description, LoadType loadType, decimal loadVa, int poles, int breakerAmps)
    {
        var circuit = Circuit.Create(slot, description, loadType, loadVa, poles, breakerAmps);
        EnsureFree(circuit.Slots, except: null);
        _circuits.Add(circuit);
        return circuit;
    }

    public void UpdateCircuit(Circuit circuit, string description, LoadType loadType, decimal loadVa, int breakerAmps)
    {
        EnsureOwns(circuit);
        circuit.Update(description, loadType, loadVa, breakerAmps);
    }

    public void MoveCircuit(Circuit circuit, int slot)
    {
        EnsureOwns(circuit);
        EnsureFree(Circuit.SlotsFrom(slot, circuit.Poles), except: circuit);
        circuit.Slot = slot;
    }

    public void RemoveCircuit(Circuit circuit)
    {
        EnsureOwns(circuit);
        _circuits.Remove(circuit);
    }

    public PanelCalculation Calculate() => PanelCalculator.Calculate(this);

    /// <summary>
    /// Rearranges circuits across slots to even out the phase loads. Keeps the current layout when
    /// the new one would not be better.
    /// </summary>
    public BalanceResult BalancePhases()
    {
        var before = Calculate().ImbalancePercent;
        var original = _circuits.ToDictionary(c => c, c => c.Slot);
        var plan = PanelCalculator.PlanBalancedSlots(Spaces, _circuits);
        if (plan is null)
        {
            return new BalanceResult(before, before, MovedCircuits: 0);
        }

        foreach (var (circuit, slot) in plan)
        {
            circuit.Slot = slot;
        }

        var after = Calculate().ImbalancePercent;
        if (after > before)
        {
            foreach (var (circuit, slot) in original)
            {
                circuit.Slot = slot;
            }

            return new BalanceResult(before, before, MovedCircuits: 0);
        }

        return new BalanceResult(before, after, original.Count(pair => pair.Key.Slot != pair.Value));
    }

    private void EnsureFree(IReadOnlyList<int> slots, Circuit? except)
    {
        var outside = slots.Where(s => s < 1 || s > Spaces).ToList();
        if (outside.Count > 0)
        {
            throw new DomainException($"Slot {outside[0]} does not exist in a {Spaces}-space panel.");
        }

        foreach (var other in _circuits.Where(c => !ReferenceEquals(c, except)))
        {
            var clash = slots.Intersect(other.Slots).FirstOrDefault();
            if (clash != 0)
            {
                throw new DomainException($"Slot {clash} is taken by circuit {other.Label} ({other.Description}).");
            }
        }
    }

    private void EnsureOwns(Circuit circuit)
    {
        if (!_circuits.Contains(circuit))
        {
            throw new DomainException($"The circuit does not belong to panel {Name}.");
        }
    }
}

public sealed record BalanceResult(decimal ImbalanceBefore, decimal ImbalanceAfter, int MovedCircuits);
