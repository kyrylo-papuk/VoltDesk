namespace VoltDesk.Infrastructure.Persistence;

/// <summary>The client sent a version token that is not a SQL Server rowversion.</summary>
public sealed class InvalidVersionException(string version)
    : Exception($"'{version}' is not a valid version token.");
