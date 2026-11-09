namespace VoltDesk.Core.Panels;

public sealed record PhaseLoad(Phase Phase, decimal Va, decimal Amps);

public sealed record CircuitLoad(Circuit Circuit, decimal Amps, bool BreakerTooSmall);

public sealed record PanelCalculation(
    IReadOnlyList<PhaseLoad> Phases,
    IReadOnlyList<CircuitLoad> Circuits,
    decimal TotalVa,
    decimal BalancedAmps,
    decimal ImbalancePercent,
    decimal BusUtilizationPercent,
    IReadOnlyList<string> Warnings)
{
    public decimal TotalKva => TotalVa / 1000m;
}

/// <summary>
/// Panel schedule arithmetic. Phase loads are summed in VA (a multi-pole load is split evenly over
/// its poles) and converted to current at line-to-neutral voltage, as panel schedules do.
/// </summary>
public static class PanelCalculator
{
    /// <summary>NEC 210.20(A) / 215.3: breakers carry continuous loads at 125%.</summary>
    public const decimal ContinuousLoadFactor = 1.25m;

    /// <summary>Above this phase imbalance the schedule suggests rebalancing.</summary>
    public const decimal ImbalanceWarningPercent = 10m;

    public const decimal Sqrt3 = 1.7320508075688772935274463415m;

    /// <summary>Current drawn by one circuit: phase-to-neutral, line-to-line or balanced three-phase.</summary>
    public static decimal CircuitAmps(VoltageSystem system, decimal loadVa, int poles) => poles switch
    {
        1 => loadVa / system.LineToNeutralVolts(),
        2 => loadVa / system.LineToLineVolts(),
        _ => loadVa / (Sqrt3 * system.LineToLineVolts()),
    };

    /// <summary>Imbalance as defined by NEMA: largest deviation from the average, over the average.</summary>
    public static decimal ImbalancePercent(IReadOnlyList<decimal> phaseAmps)
    {
        var average = phaseAmps.Average();
        return average == 0 ? 0 : phaseAmps.Max(a => Math.Abs(a - average)) / average * 100m;
    }

    public static PanelCalculation Calculate(ElectricalPanel panel)
    {
        var system = panel.System;
        var phaseVa = PhaseVa(panel.Circuits.Select(c => (c.Slots, c.LoadVa)));
        var phases = Enum.GetValues<Phase>()
            .Select(p => new PhaseLoad(p, phaseVa[(int)p], phaseVa[(int)p] / system.LineToNeutralVolts()))
            .ToList();

        var totalVa = phaseVa.Sum();
        var amps = phases.Select(p => p.Amps).ToList();
        var imbalance = ImbalancePercent(amps);
        var heaviest = phases.MaxBy(p => p.Amps)!;
        var busUtilization = heaviest.Amps / panel.BusRatingAmps * 100m;

        var circuits = panel.Circuits
            .OrderBy(c => c.Slot)
            .Select(c =>
            {
                var current = CircuitAmps(system, c.LoadVa, c.Poles);
                return new CircuitLoad(c, current, current * ContinuousLoadFactor > c.BreakerAmps);
            })
            .ToList();

        var warnings = new List<string>();
        if (busUtilization > 100)
        {
            warnings.Add($"Phase {heaviest.Phase} carries {heaviest.Amps:0.0} A, above the {panel.BusRatingAmps} A bus rating.");
        }

        if (panel.MainBreakerAmps is { } main && heaviest.Amps * ContinuousLoadFactor > main)
        {
            warnings.Add($"Phase {heaviest.Phase} at {heaviest.Amps:0.0} A × 125% exceeds the {main} A main breaker.");
        }

        foreach (var load in circuits.Where(c => c.BreakerTooSmall))
        {
            warnings.Add(
                $"Circuit {load.Circuit.Label}: {load.Amps:0.0} A × 125% = {load.Amps * ContinuousLoadFactor:0.0} A " +
                $"exceeds the {load.Circuit.BreakerAmps} A breaker.");
        }

        if (imbalance > ImbalanceWarningPercent)
        {
            warnings.Add($"Phases are {imbalance:0.#}% out of balance. Rebalancing would even them out.");
        }

        return new PanelCalculation(phases, circuits, totalVa, totalVa / (Sqrt3 * system.LineToLineVolts()), imbalance, busUtilization, warnings);
    }

    /// <summary>
    /// Greedy balancing (longest processing time first): three-pole and two-pole breakers are placed
    /// first, then single-pole loads from largest to smallest, each where it leaves the most loaded
    /// phase as light as possible. Returns null when the circuits cannot all be placed.
    /// </summary>
    public static IReadOnlyDictionary<Circuit, int>? PlanBalancedSlots(int spaces, IReadOnlyList<Circuit> circuits)
    {
        var used = new bool[spaces + 1];
        var phaseVa = new decimal[3];
        var plan = new Dictionary<Circuit, int>();

        var order = circuits
            .OrderByDescending(c => c.Poles)
            .ThenByDescending(c => c.LoadVa)
            .ThenBy(c => c.Slot);

        foreach (var circuit in order)
        {
            var share = circuit.LoadVa / circuit.Poles;
            var best = Enumerable.Range(1, spaces)
                .Select(slot => Circuit.SlotsFrom(slot, circuit.Poles))
                .Where(slots => slots.All(s => s <= spaces && !used[s]))
                .Select(slots => (Slots: slots, Score: Score(phaseVa, slots, share)))
                .OrderBy(candidate => candidate.Score.Max)
                .ThenBy(candidate => candidate.Score.Spread)
                .ThenBy(candidate => candidate.Slots[0])
                .FirstOrDefault();

            if (best.Slots is null)
            {
                return null;
            }

            foreach (var slot in best.Slots)
            {
                used[slot] = true;
                phaseVa[(int)ElectricalPanel.PhaseOf(slot)] += share;
            }

            plan[circuit] = best.Slots[0];
        }

        return plan;
    }

    private static decimal[] PhaseVa(IEnumerable<(IReadOnlyList<int> Slots, decimal LoadVa)> loads)
    {
        var phaseVa = new decimal[3];
        foreach (var (slots, loadVa) in loads)
        {
            foreach (var slot in slots)
            {
                phaseVa[(int)ElectricalPanel.PhaseOf(slot)] += loadVa / slots.Count;
            }
        }

        return phaseVa;
    }

    private static (decimal Max, decimal Spread) Score(decimal[] phaseVa, IReadOnlyList<int> slots, decimal share)
    {
        var after = (decimal[])phaseVa.Clone();
        foreach (var slot in slots)
        {
            after[(int)ElectricalPanel.PhaseOf(slot)] += share;
        }

        return (after.Max(), after.Max() - after.Min());
    }
}
