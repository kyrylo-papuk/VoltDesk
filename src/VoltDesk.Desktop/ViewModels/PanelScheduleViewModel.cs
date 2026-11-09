using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Core.Panels;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>One slot of the two-sided schedule.</summary>
public sealed record SlotCell(int Slot, CircuitDto? Circuit)
{
    public bool IsStart => Circuit?.Slot == Slot;

    public bool IsEmpty => Circuit is null;

    public string Number => Slot.ToString();

    public string Text => Circuit switch
    {
        null => string.Empty,
        _ when IsStart => Circuit.Description,
        _ => $"↳ {Circuit.Description}",
    };

    public string Load => Circuit is null ? string.Empty : (Circuit.LoadVa / Circuit.Poles).ToString("#,0");

    public string Breaker => Circuit is { } c && IsStart ? $"{c.BreakerAmps}A/{c.Poles}P" : string.Empty;

    public bool IsWarning => Circuit?.BreakerTooSmall == true;

    public string AutomationKey => $"Slot {Slot}";
}

public sealed record ScheduleRow(Phase Phase, SlotCell Left, SlotCell Right);

public sealed record PhaseBar(PhaseLoadDto Load, int BusRatingAmps)
{
    public double Percent => BusRatingAmps == 0 ? 0 : Math.Min(100, (double)(Load.Amps / BusRatingAmps * 100));

    public string Text => $"{Load.Amps:0.0} A · {Load.Va:#,0} VA";

    /// <summary>Bar colour: over the bus rating, above 80% of it, or normal.</summary>
    public string Level => Percent >= 100 ? "Overload" : Percent > 80 ? "Heavy" : "Normal";
}

public sealed partial class PanelScheduleViewModel(ApiClient api, Navigator navigator, DialogService dialogs) : PageViewModel
{
    private static readonly string ExcelFilter = "Excel workbook (*.xlsx)|*.xlsx";

    public override string Section => "Projects";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(TotalKva), nameof(IsImbalanced), nameof(HasWarnings))]
    public partial PanelDto? Panel { get; set; }

    public decimal TotalKva => (Panel?.Calculation.TotalVa ?? 0) / 1000m;

    public bool IsImbalanced => Panel?.Calculation.ImbalancePercent > PanelCalculator.ImbalanceWarningPercent;

    public bool HasWarnings => Panel?.Calculation.Warnings.Count > 0;

    public ObservableCollection<ScheduleRow> Rows { get; } = [];

    public ObservableCollection<PhaseBar> PhaseBars { get; } = [];

    public IReadOnlyList<LoadType> LoadTypes { get; } = Enum.GetValues<LoadType>();

    public IReadOnlyList<int> PoleOptions { get; } = [1, 2, 3];

    public IReadOnlyList<int> BreakerOptions { get; } = Circuit.StandardBreakerAmps;

    public string Subtitle => Panel is null
        ? string.Empty
        : $"{Panel.ProjectNumber} · {Panel.System.Label()} · Bus {Panel.BusRatingAmps} A · " +
          (Panel.MainBreakerAmps is { } main ? $"Main {main} A" : "Main lugs only") +
          $" · {Panel.Spaces} spaces" + (Panel.Location is null ? string.Empty : $" · {Panel.Location}");

    // Circuit editor
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(EditorTitle))]
    public partial CircuitDto? EditingCircuit { get; set; }

    [ObservableProperty]
    public partial int EditSlot { get; set; } = 1;

    [ObservableProperty]
    public partial string EditDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial LoadType EditLoadType { get; set; } = LoadType.Receptacles;

    [ObservableProperty]
    public partial decimal EditLoadVa { get; set; }

    [ObservableProperty]
    public partial int EditPoles { get; set; } = 1;

    [ObservableProperty]
    public partial int EditBreakerAmps { get; set; } = 20;

    public bool IsEditing => EditingCircuit is not null;

    public string EditorTitle => EditingCircuit is { } c ? $"Circuit {c.Label}" : "New circuit";

    public Task LoadAsync(int panelId) => RunAsync(async () => Show(await api.GetPanelAsync(panelId)));

    public override Task LoadAsync() => Panel is null ? Task.CompletedTask : LoadAsync(Panel.Id);

    [RelayCommand]
    private Task BackAsync() => navigator.GoBackAsync();

    [RelayCommand]
    private void SelectSlot(SlotCell cell)
    {
        if (cell.Circuit is { } circuit)
        {
            EditingCircuit = circuit;
            EditSlot = circuit.Slot;
            EditDescription = circuit.Description;
            EditLoadType = circuit.LoadType;
            EditLoadVa = circuit.LoadVa;
            EditPoles = circuit.Poles;
            EditBreakerAmps = circuit.BreakerAmps;
        }
        else
        {
            NewCircuit();
            EditSlot = cell.Slot;
        }
    }

    [RelayCommand]
    private void NewCircuit()
    {
        EditingCircuit = null;
        EditDescription = string.Empty;
        EditLoadType = LoadType.Receptacles;
        EditLoadVa = 0;
        EditPoles = 1;
        EditBreakerAmps = 20;
    }

    [RelayCommand]
    private Task SaveCircuitAsync() => RunAsync(
        async () =>
        {
            var request = new CircuitRequest(EditSlot, EditDescription.Trim(), EditLoadType, EditLoadVa, EditPoles, EditBreakerAmps, Panel!.Version);
            var updated = EditingCircuit is { } circuit
                ? await api.UpdateCircuitAsync(Panel.Id, circuit.Id, request)
                : await api.AddCircuitAsync(Panel.Id, request);
            Show(updated);
            InfoMessage = $"Circuit {EditSlot} saved.";
            EditingCircuit = updated.Circuits.FirstOrDefault(c => c.Slot == EditSlot);
        },
        onConflict: ReloadAsync);

    [RelayCommand]
    private Task RemoveCircuitAsync() => RunAsync(
        async () =>
        {
            var circuit = EditingCircuit!;
            Show(await api.RemoveCircuitAsync(Panel!.Id, circuit.Id, Panel.Version));
            InfoMessage = $"Circuit {circuit.Label} removed.";
            NewCircuit();
        },
        onConflict: ReloadAsync);

    [RelayCommand]
    private Task BalanceAsync() => RunAsync(
        async () =>
        {
            var result = await api.BalancePanelAsync(Panel!.Id, Panel.Version);
            Show(result.Panel);
            InfoMessage = result.MovedCircuits == 0
                ? $"Already as balanced as it gets ({result.ImbalanceAfter:0.#}% imbalance)."
                : $"Moved {result.MovedCircuits} circuits: imbalance {result.ImbalanceBefore:0.#}% → {result.ImbalanceAfter:0.#}%.";
            NewCircuit();
        },
        onConflict: ReloadAsync);

    [RelayCommand]
    private Task ExportAsync() => RunAsync(async () =>
    {
        var (content, fileName) = await api.ExportPanelAsync(Panel!.Id);
        if (dialogs.SaveFile(content, fileName, ExcelFilter) is { } path)
        {
            InfoMessage = $"Saved {path}";
        }
    });

    private async Task ReloadAsync() => Show(await api.GetPanelAsync(Panel!.Id));

    private void Show(PanelDto panel)
    {
        Panel = panel;

        PhaseBars.Clear();
        foreach (var phase in panel.Calculation.Phases)
        {
            PhaseBars.Add(new PhaseBar(phase, panel.BusRatingAmps));
        }

        var bySlot = panel.Circuits
            .SelectMany(c => c.Slots.Select(slot => (slot, c)))
            .ToDictionary(pair => pair.slot, pair => pair.c);

        Rows.Clear();
        for (var left = 1; left <= panel.Spaces; left += 2)
        {
            Rows.Add(new ScheduleRow(
                ElectricalPanel.PhaseOf(left),
                new SlotCell(left, bySlot.GetValueOrDefault(left)),
                new SlotCell(left + 1, bySlot.GetValueOrDefault(left + 1))));
        }
    }
}
