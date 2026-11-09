using ClosedXML.Excel;
using VoltDesk.Core.Panels;

namespace VoltDesk.Infrastructure.Export;

/// <summary>
/// Builds a panel schedule workbook. Phase totals are Excel formulas, so the sheet stays correct
/// when an engineer edits a load by hand.
/// </summary>
public static class PanelScheduleWorkbook
{
    private const int HeaderRow = 6;
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#0F172A");
    private static readonly XLColor TotalsFill = XLColor.FromHtml("#F1F5F9");
    private static readonly XLColor WarningFont = XLColor.FromHtml("#B91C1C");

    public static byte[] Build(ElectricalPanel panel, string projectNumber, string projectName)
    {
        var calculation = panel.Calculate();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(panel.Name));

        sheet.Cell(1, 1).Value = $"Panel schedule {panel.Name}";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = $"Project {projectNumber} · {projectName}";
        sheet.Cell(3, 1).Value =
            $"{panel.System.Label()} · Bus {panel.BusRatingAmps} A · " +
            (panel.MainBreakerAmps is { } main ? $"Main breaker {main} A" : "Main lugs only") +
            $" · {panel.Spaces} spaces";
        sheet.Cell(4, 1).Value = panel.Location ?? string.Empty;
        sheet.Range(2, 1, 4, 1).Style.Font.SetFontColor(XLColor.FromHtml("#475569"));

        string[] headers = ["Ckt", "Description", "Load type", "Load (VA)", "Poles", "Breaker (A)", "Phases", "A (VA)", "B (VA)", "C (VA)", "Current (A)"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(HeaderRow, i + 1).Value = headers[i];
        }

        var header = sheet.Range(HeaderRow, 1, HeaderRow, headers.Length);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(HeaderFill);

        var row = HeaderRow + 1;
        foreach (var load in calculation.Circuits)
        {
            var circuit = load.Circuit;
            sheet.Cell(row, 1).Value = circuit.Label;
            sheet.Cell(row, 2).Value = circuit.Description;
            sheet.Cell(row, 3).Value = circuit.LoadType.ToString();
            sheet.Cell(row, 4).Value = circuit.LoadVa;
            sheet.Cell(row, 5).Value = circuit.Poles;
            sheet.Cell(row, 6).Value = circuit.BreakerAmps;
            sheet.Cell(row, 7).Value = string.Concat(circuit.Slots.Select(ElectricalPanel.PhaseOf));

            // Each pole carries an equal share of the load: =D7/E7 in the columns of its phases.
            foreach (var phase in circuit.Slots.Select(ElectricalPanel.PhaseOf))
            {
                sheet.Cell(row, 8 + (int)phase).FormulaA1 = $"=D{row}/E{row}";
            }

            sheet.Cell(row, 11).Value = Math.Round(load.Amps, 1);
            if (load.BreakerTooSmall)
            {
                sheet.Range(row, 1, row, headers.Length).Style.Font.SetFontColor(WarningFont);
            }

            row++;
        }

        var lastCircuitRow = row - 1;
        var totals = row;
        sheet.Cell(totals, 2).Value = "Connected load";
        sheet.Cell(totals, 4).FormulaA1 = calculation.Circuits.Count == 0 ? "=0" : $"=SUM(D{HeaderRow + 1}:D{lastCircuitRow})";
        for (var column = 8; column <= 10; column++)
        {
            var letter = XLHelper.GetColumnLetterFromNumber(column);
            sheet.Cell(totals, column).FormulaA1 = calculation.Circuits.Count == 0 ? "=0" : $"=SUM({letter}{HeaderRow + 1}:{letter}{lastCircuitRow})";
        }

        sheet.Range(totals, 1, totals, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(TotalsFill);

        // Phase currents follow the VA totals: VA / line-to-neutral voltage.
        var volts = panel.System.LineToNeutralVolts();
        sheet.Cell(totals + 1, 2).Value = $"Phase current at {volts} V (A)";
        for (var column = 8; column <= 10; column++)
        {
            var letter = XLHelper.GetColumnLetterFromNumber(column);
            sheet.Cell(totals + 1, column).FormulaA1 = $"=ROUND({letter}{totals}/{volts},1)";
        }

        var summary = totals + 3;
        WriteSummary(sheet, summary, "Total connected load (kVA)", Math.Round(calculation.TotalKva, 2));
        WriteSummary(sheet, summary + 1, "Balanced three-phase current (A)", Math.Round(calculation.BalancedAmps, 1));
        WriteSummary(sheet, summary + 2, "Phase imbalance (%)", Math.Round(calculation.ImbalancePercent, 1));
        WriteSummary(sheet, summary + 3, "Bus utilization (%)", Math.Round(calculation.BusUtilizationPercent, 1));

        var warningsRow = summary + 5;
        sheet.Cell(warningsRow, 1).Value = calculation.Warnings.Count == 0 ? "No warnings." : "Warnings";
        sheet.Cell(warningsRow, 1).Style.Font.SetBold();
        for (var i = 0; i < calculation.Warnings.Count; i++)
        {
            sheet.Cell(warningsRow + 1 + i, 1).Value = calculation.Warnings[i];
            sheet.Cell(warningsRow + 1 + i, 1).Style.Font.SetFontColor(WarningFont);
        }

        sheet.Range(HeaderRow + 1, 4, totals, 4).Style.NumberFormat.Format = "#,##0";
        sheet.Range(HeaderRow + 1, 8, totals, 10).Style.NumberFormat.Format = "#,##0";
        sheet.Range(HeaderRow, 1, totals + 1, headers.Length).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        sheet.Range(HeaderRow, 1, totals + 1, headers.Length).Style.Border.SetInsideBorder(XLBorderStyleValues.Hair);
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.Column(1).Width = 9;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Excel sheet names: at most 31 characters, none of : \ / ? * [ ].
    private static string SheetName(string name) =>
        new string([.. name.Select(ch => @":\/?*[]".Contains(ch) ? '-' : ch)])[..Math.Min(name.Length, 31)];

    private static void WriteSummary(IXLWorksheet sheet, int row, string label, decimal value)
    {
        sheet.Cell(row, 2).Value = label;
        sheet.Cell(row, 4).Value = value;
        sheet.Cell(row, 2).Style.Font.SetBold();
    }
}
