using VoltDesk.Core.Clients;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;

namespace VoltDesk.Core.Tests;

internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 11, 9);

    public static Employee Manager() => Employee.Create("Dana Whitfield", "dana@voltdesk.test", EmployeeRole.ProjectManager);

    public static Employee Engineer() => Employee.Create("Sam Ortega", "sam@voltdesk.test", EmployeeRole.Engineer);

    public static Client Client() => Clients.Client.Create("Cedar Valley Medical Center");

    public static Project Project() => Projects.Project.Create(
        "2026-014",
        "Surgical Wing Expansion",
        Client(),
        BuildingType.Healthcare,
        "Denver",
        "CO",
        Manager(),
        fee: 185_000m);

    /// <summary>Adds a task in the current stage and takes it to Done.</summary>
    public static ProjectTask AddDoneTask(Project project, string title = "Single-line diagram")
    {
        var task = project.AddTask(title, Discipline.Power);
        task.AssignTo(Engineer());
        task.MoveTo(ProjectTaskStatus.Done, Today);
        return task;
    }
}
