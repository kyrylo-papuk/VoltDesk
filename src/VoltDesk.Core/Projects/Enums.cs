namespace VoltDesk.Core.Projects;

public enum BuildingType
{
    Healthcare,
    DataCenter,
    Industrial,
    Commercial,
    Education,
    Residential,
}

public enum ProjectStatus
{
    Active,
    OnHold,
    Completed,
    Cancelled,
}

/// <summary>Electrical sub-discipline a task belongs to.</summary>
public enum Discipline
{
    Power,
    Lighting,
    FireAlarm,
    LowVoltage,
    General,
}

public enum TaskPriority
{
    Low,
    Normal,
    High,
}

public enum ProjectTaskStatus
{
    ToDo,
    InProgress,
    InReview,
    Done,
}
