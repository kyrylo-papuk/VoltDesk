namespace VoltDesk.Infrastructure;

/// <summary>Who is making the current change. Recorded in the audit log.</summary>
public interface ICurrentActor
{
    int? EmployeeId { get; }
}

/// <summary>Changes made by the system itself: migrations, demo data, background jobs.</summary>
internal sealed class SystemActor : ICurrentActor
{
    public int? EmployeeId => null;
}
