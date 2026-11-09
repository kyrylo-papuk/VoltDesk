using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VoltDesk.Core.Projects;

namespace VoltDesk.Desktop.Converters;

/// <summary>"InProgress" → "In progress"; with parameter "abbr", design stages become "SD", "DD"…</summary>
public sealed partial class EnumDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => null,
        DesignStage stage when (parameter as string) == "abbr" => stage.Abbreviation(),
        DesignStage stage => stage.DisplayName(),
        Enum e => Humanize(e.ToString()),
        _ => value.ToString(),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static string Humanize(string pascalCase)
    {
        var words = WordBoundary().Replace(pascalCase, " $1").Trim();
        return words.Length == 0 ? words : words[0] + words[1..].ToLowerInvariant();
    }

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}

/// <summary>Badge colours by status name. Parameter "fg" returns the text colour, otherwise the background.</summary>
public sealed class StatusBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, (Brush Background, Brush Foreground)> Palette = new()
    {
        ["Active"] = Pair("#DCFCE7", "#15803D"),
        ["Done"] = Pair("#DCFCE7", "#15803D"),
        ["OnHold"] = Pair("#FEF3C7", "#B45309"),
        ["InReview"] = Pair("#FEF3C7", "#B45309"),
        ["InProgress"] = Pair("#DBEAFE", "#1D4ED8"),
        ["Current"] = Pair("#DBEAFE", "#1D4ED8"),
        ["Cancelled"] = Pair("#FEE2E2", "#B91C1C"),
        ["High"] = Pair("#FEE2E2", "#B91C1C"),
        ["Overdue"] = Pair("#FEE2E2", "#B91C1C"),
    };

    private static readonly (Brush Background, Brush Foreground) Neutral = Pair("#F1F5F9", "#475569");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var colours = value is not null && Palette.TryGetValue(value.ToString()!, out var found) ? found : Neutral;
        return (parameter as string) == "fg" ? colours.Foreground : colours.Background;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static (Brush, Brush) Pair(string background, string foreground) => (Freeze(background), Freeze(foreground));

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Visible when the value is true, a non-empty string or any other non-null object. "invert" flips it.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool flag => flag,
            string text => text.Length > 0,
            int count => count > 0,
            _ => true,
        };
        if ((parameter as string) == "invert")
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bridges <see cref="DateOnly"/> view-model properties and <c>DatePicker.SelectedDate</c>.</summary>
public sealed class DateOnlyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : null;
}

/// <summary>True when the bound value equals the parameter; drives the active sidebar item.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
