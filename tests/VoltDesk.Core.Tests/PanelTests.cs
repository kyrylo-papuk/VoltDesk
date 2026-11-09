using VoltDesk.Core.Common;
using VoltDesk.Core.Panels;

namespace VoltDesk.Core.Tests;

public sealed class PanelTests
{
    private static ElectricalPanel Panel(VoltageSystem system = VoltageSystem.ThreePhase208Y120, int bus = 225, int? main = 200) =>
        ElectricalPanel.Create(projectId: 1, "lp-2a", "Level 2 electrical room", system, bus, main, spaces: 42);

    [Theory]
    [InlineData(1, Phase.A)]
    [InlineData(2, Phase.A)]
    [InlineData(3, Phase.B)]
    [InlineData(4, Phase.B)]
    [InlineData(5, Phase.C)]
    [InlineData(6, Phase.C)]
    [InlineData(7, Phase.A)]
    [InlineData(41, Phase.C)]
    [InlineData(42, Phase.C)]
    public void Rows_of_two_slots_alternate_phases_a_b_c(int slot, Phase phase)
    {
        Assert.Equal(phase, ElectricalPanel.PhaseOf(slot));
    }

    [Fact]
    public void Multi_pole_breaker_takes_slots_on_the_same_side()
    {
        var panel = Panel();

        var circuit = panel.AddCircuit(2, "RTU-1", LoadType.Mechanical, 9000m, poles: 3, breakerAmps: 40);

        Assert.Equal([2, 4, 6], circuit.Slots);
        Assert.Equal("2,4,6", circuit.Label);
        Assert.Equal("LP-2A", panel.Name);
    }

    [Fact]
    public void Slots_cannot_overlap()
    {
        var panel = Panel();
        panel.AddCircuit(1, "Water heater", LoadType.Equipment, 4500m, poles: 2, breakerAmps: 30);

        var error = Assert.Throws<DomainException>(
            () => panel.AddCircuit(3, "Receptacles", LoadType.Receptacles, 900m, poles: 1, breakerAmps: 20));

        Assert.Contains("Slot 3 is taken by circuit 1,3", error.Message);
    }

    [Fact]
    public void Breaker_must_fit_in_the_panel()
    {
        var panel = Panel();

        Assert.Throws<DomainException>(() => panel.AddCircuit(41, "Chiller", LoadType.Mechanical, 30000m, poles: 3, breakerAmps: 125));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(0)]
    public void Breaker_rating_must_be_standard(int amps)
    {
        Assert.Throws<DomainException>(() => Panel().AddCircuit(1, "Lighting", LoadType.Lighting, 500m, 1, amps));
    }

    [Fact]
    public void Main_breaker_cannot_exceed_the_bus()
    {
        Assert.Throws<DomainException>(() => Panel(bus: 100, main: 225));
    }

    [Fact]
    public void Moving_a_circuit_onto_another_is_rejected_and_a_free_slot_works()
    {
        var panel = Panel();
        var lighting = panel.AddCircuit(1, "Lighting", LoadType.Lighting, 1200m, 1, 20);
        panel.AddCircuit(3, "Receptacles", LoadType.Receptacles, 900m, 1, 20);

        Assert.Throws<DomainException>(() => panel.MoveCircuit(lighting, 3));
        panel.MoveCircuit(lighting, 5);

        Assert.Equal(5, lighting.Slot);
    }

    [Fact]
    public void Single_pole_load_on_208Y120_draws_va_over_120_volts()
    {
        var panel = Panel();
        panel.AddCircuit(1, "Lighting", LoadType.Lighting, 1200m, 1, 20);

        var result = panel.Calculate();

        Assert.Equal(1200m, result.Phases[0].Va);
        Assert.Equal(10m, result.Phases[0].Amps);
        Assert.Equal(0m, result.Phases[1].Va);
        Assert.Equal(1.2m, result.TotalKva);
        Assert.Equal(3.33m, Math.Round(result.BalancedAmps, 2));
    }

    [Fact]
    public void Single_pole_load_on_480Y277_uses_277_volts()
    {
        var panel = Panel(VoltageSystem.ThreePhase480Y277, bus: 400, main: null);
        panel.AddCircuit(1, "Corridor lighting", LoadType.Lighting, 2770m, 1, 20);

        Assert.Equal(10m, panel.Calculate().Phases[0].Amps);
    }

    [Fact]
    public void Multi_pole_load_is_split_evenly_over_its_phases()
    {
        var panel = Panel();
        panel.AddCircuit(1, "Water heater", LoadType.Equipment, 4500m, poles: 2, breakerAmps: 30);
        panel.AddCircuit(6, "RTU-1", LoadType.Mechanical, 9000m, poles: 3, breakerAmps: 40);

        var phases = panel.Calculate().Phases;

        Assert.Equal(2250m + 3000m, phases[0].Va);
        Assert.Equal(2250m + 3000m, phases[1].Va);
        Assert.Equal(3000m, phases[2].Va);
    }

    [Fact]
    public void Imbalance_is_the_largest_deviation_from_the_average()
    {
        Assert.Equal(50m, PanelCalculator.ImbalancePercent([30m, 20m, 10m]));
        Assert.Equal(0m, PanelCalculator.ImbalancePercent([0m, 0m, 0m]));
    }

    [Theory]
    [InlineData(1800, false)]
    [InlineData(1920, false)]
    [InlineData(2400, true)]
    public void Breaker_must_carry_the_load_at_125_percent(int loadVa, bool tooSmall)
    {
        var panel = Panel();
        panel.AddCircuit(1, "Receptacles", LoadType.Receptacles, loadVa, 1, 20);

        var result = panel.Calculate();

        Assert.Equal(tooSmall, result.Circuits[0].BreakerTooSmall);
        Assert.Equal(tooSmall, result.Warnings.Any(w => w.StartsWith("Circuit 1:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Overloaded_bus_and_main_breaker_are_reported()
    {
        var panel = Panel(bus: 100, main: 100);
        for (var slot = 1; slot <= 41; slot += 6)
        {
            panel.AddCircuit(slot, $"Heater {slot}", LoadType.Equipment, 2000m, 1, 25);
        }

        var warnings = panel.Calculate().Warnings;

        Assert.Contains(warnings, w => w.Contains("above the 100 A bus rating"));
        Assert.Contains(warnings, w => w.Contains("exceeds the 100 A main breaker"));
    }

    [Fact]
    public void Balancing_evens_out_a_lopsided_panel_and_keeps_every_circuit()
    {
        var panel = Panel();
        // Everything sits on phase A slots: 1, 7, 13, 19, 25, 31.
        decimal[] loads = [1800m, 1500m, 1200m, 1000m, 900m, 600m];
        for (var i = 0; i < loads.Length; i++)
        {
            panel.AddCircuit(1 + (6 * i), $"Load {i}", LoadType.Receptacles, loads[i], 1, 20);
        }

        panel.AddCircuit(2, "RTU-1", LoadType.Mechanical, 9000m, poles: 3, breakerAmps: 40);
        var before = panel.Calculate();

        var result = panel.BalancePhases();
        var after = panel.Calculate();

        Assert.Equal(87.5m, Math.Round(before.ImbalancePercent, 1));
        Assert.True(after.ImbalancePercent < 10m, $"after: {after.ImbalancePercent:0.0}%");
        Assert.Equal(result.ImbalanceAfter, after.ImbalancePercent);
        Assert.Equal(before.TotalVa, after.TotalVa);
        Assert.True(result.MovedCircuits > 0);

        var slots = panel.Circuits.SelectMany(c => c.Slots).ToList();
        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.All(slots, s => Assert.InRange(s, 1, panel.Spaces));
        var rtu = panel.Circuits.Single(c => c.Poles == 3);
        Assert.Equal(3, rtu.Slots.Select(ElectricalPanel.PhaseOf).Distinct().Count());
    }

    [Fact]
    public void Balancing_a_balanced_panel_does_not_make_it_worse()
    {
        var panel = Panel();
        panel.AddCircuit(1, "A load", LoadType.Lighting, 1000m, 1, 20);
        panel.AddCircuit(3, "B load", LoadType.Lighting, 1000m, 1, 20);
        panel.AddCircuit(5, "C load", LoadType.Lighting, 1000m, 1, 20);

        var result = panel.BalancePhases();

        Assert.Equal(0m, result.ImbalanceAfter);
        Assert.True(result.ImbalanceAfter <= result.ImbalanceBefore);
    }
}
