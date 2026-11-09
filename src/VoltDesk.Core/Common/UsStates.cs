using System.Collections.Frozen;

namespace VoltDesk.Core.Common;

public static class UsStates
{
    private static readonly FrozenSet<string> Codes = new[]
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
        "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
        "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
        "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
        "DC",
    }.ToFrozenSet();

    public static IReadOnlyCollection<string> All => Codes;

    public static bool IsValid(string? code) =>
        code is not null && Codes.Contains(code.Trim().ToUpperInvariant());

    internal static string Normalize(string? code) =>
        IsValid(code)
            ? code!.Trim().ToUpperInvariant()
            : throw new DomainException($"'{code}' is not a US state code.");
}
