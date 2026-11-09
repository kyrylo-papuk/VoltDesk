using System.ComponentModel.DataAnnotations;
using VoltDesk.Core.Panels;

namespace VoltDesk.Contracts;

public sealed record PanelSummaryDto(
    int Id,
    int ProjectId,
    string Name,
    string? Location,
    VoltageSystem System,
    int BusRatingAmps,
    int CircuitCount,
    decimal TotalKva,
    decimal ImbalancePercent,
    int WarningCount);

public sealed record PanelDto(
    int Id,
    int ProjectId,
    string ProjectNumber,
    string Name,
    string? Location,
    VoltageSystem System,
    int BusRatingAmps,
    int? MainBreakerAmps,
    int Spaces,
    IReadOnlyList<CircuitDto> Circuits,
    PanelCalculationDto Calculation,
    string Version);

public sealed record CircuitDto(
    int Id,
    int Slot,
    IReadOnlyList<int> Slots,
    string Label,
    string Description,
    LoadType LoadType,
    decimal LoadVa,
    int Poles,
    int BreakerAmps,
    decimal Amps,
    bool BreakerTooSmall);

public sealed record PhaseLoadDto(Phase Phase, decimal Va, decimal Amps);

public sealed record PanelCalculationDto(
    IReadOnlyList<PhaseLoadDto> Phases,
    decimal TotalVa,
    decimal BalancedAmps,
    decimal ImbalancePercent,
    decimal BusUtilizationPercent,
    IReadOnlyList<string> Warnings);

public sealed record CreatePanelRequest(
    [Required, StringLength(ElectricalPanel.NameMaxLength)] string Name,
    [StringLength(ElectricalPanel.LocationMaxLength)] string? Location,
    VoltageSystem System,
    int BusRatingAmps,
    int? MainBreakerAmps,
    int Spaces);

public sealed record UpdatePanelRequest(
    [Required, StringLength(ElectricalPanel.NameMaxLength)] string Name,
    [StringLength(ElectricalPanel.LocationMaxLength)] string? Location,
    int BusRatingAmps,
    int? MainBreakerAmps,
    [Required] string Version);

/// <summary>Adds or edits a circuit. <see cref="Version"/> is the panel's version; poles are fixed once added.</summary>
public sealed record CircuitRequest(
    [Range(1, 84)] int Slot,
    [Required, StringLength(Circuit.DescriptionMaxLength)] string Description,
    LoadType LoadType,
    [Range(0, 1_000_000)] decimal LoadVa,
    [Range(1, 3)] int Poles,
    int BreakerAmps,
    [Required] string Version);

public sealed record BalanceResultDto(decimal ImbalanceBefore, decimal ImbalanceAfter, int MovedCircuits, PanelDto Panel);
