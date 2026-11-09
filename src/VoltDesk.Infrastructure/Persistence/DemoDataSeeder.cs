using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Panels;
using VoltDesk.Core.Projects;

namespace VoltDesk.Infrastructure.Persistence;

/// <summary>
/// Fills an empty database with a believable demo studio. All companies and people are fictional.
/// Data is built through domain methods, so it obeys the same rules as data entered by users.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Password of every demo account. Demo data is only seeded in development and tests.</summary>
    public const string DemoPassword = "VoltDesk-demo-2026";

    public static async Task SeedAsync(
        VoltDeskDbContext db,
        IPasswordHasher<Employee> hasher,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        if (await db.Employees.AnyAsync(cancellationToken))
        {
            return;
        }

        var dana = Employee.Create("Dana Whitfield", "dana.whitfield@voltdesk.example", EmployeeRole.ProjectManager);
        var marcus = Employee.Create("Marcus Bell", "marcus.bell@voltdesk.example", EmployeeRole.ProjectManager);
        var avery = Employee.Create("Avery Stone", "avery.stone@voltdesk.example", EmployeeRole.Admin);
        var sam = Employee.Create("Sam Ortega", "sam.ortega@voltdesk.example", EmployeeRole.Engineer);
        var priya = Employee.Create("Priya Nair", "priya.nair@voltdesk.example", EmployeeRole.Engineer);
        var tom = Employee.Create("Tom Kowalski", "tom.kowalski@voltdesk.example", EmployeeRole.Engineer);
        var lena = Employee.Create("Lena Hoffman", "lena.hoffman@voltdesk.example", EmployeeRole.Engineer);
        var ray = Employee.Create("Ray Mendez", "ray.mendez@voltdesk.example", EmployeeRole.Engineer);
        ray.Deactivate();
        foreach (var employee in new[] { dana, marcus, avery, sam, priya, tom, lena, ray })
        {
            employee.SetPasswordHash(hasher.HashPassword(employee, DemoPassword));
        }

        var cedarValley = Client.Create("Cedar Valley Health System");
        cedarValley.AddContact("Mia Chen", "Facilities Director", "mia.chen@cedarvalley.example", "+1 303 555 0142");
        cedarValley.AddContact("Robert Hale", "Construction Manager", "robert.hale@cedarvalley.example", null);

        var ironbridge = Client.Create("Ironbridge Data Centers");
        ironbridge.AddContact("Kevin Duarte", "VP, Critical Facilities", "kevin.duarte@ironbridge.example", "+1 214 555 0177");

        var summit = Client.Create("Summit Precision Manufacturing");
        summit.AddContact("Laura Brooks", "Plant Engineer", "laura.brooks@summitprecision.example", "+1 614 555 0108");

        var harborview = Client.Create("Harborview Properties");
        harborview.AddContact("Nate Fischer", "Development Manager", "nate.fischer@harborview.example", null);

        var lakeshore = Client.Create("Lakeshore School District");
        lakeshore.AddContact("Grace Kim", "Capital Projects Coordinator", null, "+1 608 555 0131");

        // Active, Design Development.
        var surgicalWing = Project.Create(
            "2026-014", "Surgical Wing Expansion", cedarValley, BuildingType.Healthcare, "Denver", "CO", dana, 185_000m);
        Schedule(surgicalWing, today, -30, 21, 75, 110, 300);
        CompleteStage(surgicalWing, today.AddDays(-32), lena, "Utility service coordination", "SD narrative and load estimate");
        AddTask(surgicalWing, today, "Single-line diagram", Discipline.Power, sam, ProjectTaskStatus.InProgress, TaskPriority.High, 10, 40);
        AddTask(surgicalWing, today, "Essential electrical system branch separation", Discipline.Power, priya, ProjectTaskStatus.InReview, TaskPriority.High, 4, 24);
        AddTask(surgicalWing, today, "Lighting layout, operating rooms", Discipline.Lighting, lena, ProjectTaskStatus.ToDo, TaskPriority.Normal, 14, 32);
        AddTask(surgicalWing, today, "Generator sizing", Discipline.Power, null, ProjectTaskStatus.ToDo, TaskPriority.High, 7, 16);
        AddTask(surgicalWing, today, "Nurse call coordination", Discipline.LowVoltage, null, ProjectTaskStatus.ToDo, TaskPriority.Low, 20, 8);

        // Active, Construction Documents.
        var dataHall = Project.Create(
            "2026-017", "DAL-2 Data Hall Fit-out", ironbridge, BuildingType.DataCenter, "Dallas", "TX", marcus, 420_000m);
        Schedule(dataHall, today, -90, -40, 18, 45, 240);
        CompleteStage(dataHall, today.AddDays(-92), tom, "Redundancy concept (2N)");
        CompleteStage(dataHall, today.AddDays(-41), priya, "UPS and PDU selection", "Cable tray routing");
        AddTask(dataHall, today, "UPS distribution one-line", Discipline.Power, tom, ProjectTaskStatus.InProgress, TaskPriority.High, 6, 30);
        AddTask(dataHall, today, "Short-circuit and arc flash study", Discipline.Power, sam, ProjectTaskStatus.ToDo, TaskPriority.High, 12, 48);
        AddTask(dataHall, today, "Panel schedules, data hall A", Discipline.Power, priya, ProjectTaskStatus.InReview, TaskPriority.Normal, 3, 20);
        AddTask(dataHall, today, "Busway layout", Discipline.Power, null, ProjectTaskStatus.ToDo, TaskPriority.Normal, 15, 24);
        AddTask(dataHall, today, "Fire alarm and VESDA coordination", Discipline.FireAlarm, lena, ProjectTaskStatus.Done, TaskPriority.Normal, -2, 12);

        // Active, Permitting.
        var plantUpgrade = Project.Create(
            "2026-009", "Plant 3 Power Upgrade", summit, BuildingType.Industrial, "Columbus", "OH", dana, 96_500m);
        Schedule(plantUpgrade, today, -150, -100, -45, 9, 120);
        CompleteStage(plantUpgrade, today.AddDays(-151), sam, "Existing switchgear survey");
        CompleteStage(plantUpgrade, today.AddDays(-99), sam, "Motor control center sizing");
        CompleteStage(plantUpgrade, today.AddDays(-47), tom, "Permit drawing set", "Coordination study");
        AddTask(plantUpgrade, today, "Respond to plan review comments", Discipline.General, tom, ProjectTaskStatus.InProgress, TaskPriority.High, 5, 10);

        // Active, Schematic Design.
        var towerFitOut = Project.Create(
            "2026-021", "Harborview Tower Floors 12-14", harborview, BuildingType.Commercial, "Seattle", "WA", marcus, 58_000m);
        Schedule(towerFitOut, today, 12, 40, 70, 95, 200);
        AddTask(towerFitOut, today, "Existing conditions survey", Discipline.General, lena, ProjectTaskStatus.Done, TaskPriority.Normal, -5, 12);
        AddTask(towerFitOut, today, "Lighting concept and controls narrative", Discipline.Lighting, lena, ProjectTaskStatus.InProgress, TaskPriority.Normal, 8, 16);
        AddTask(towerFitOut, today, "Preliminary load calculation", Discipline.Power, null, ProjectTaskStatus.ToDo, TaskPriority.Normal, 10, 8);

        // On hold in Design Development: the client paused funding.
        var medicalOffice = Project.Create(
            "2026-019", "Aurora Medical Office Building", cedarValley, BuildingType.Healthcare, "Aurora", "CO", marcus, 132_000m);
        Schedule(medicalOffice, today, -60, -5, 60, 90, 280);
        CompleteStage(medicalOffice, today.AddDays(-62), priya, "Service size and utility application");
        AddTask(medicalOffice, today, "Imaging suite power requirements", Discipline.Power, priya, ProjectTaskStatus.InProgress, TaskPriority.Normal, -5, 20);
        medicalOffice.PutOnHold();

        // Completed last year.
        var school = Project.Create(
            "2025-031", "Lakeshore Elementary Renovation", lakeshore, BuildingType.Education, "Madison", "WI", dana, 74_000m);
        Schedule(school, today, -400, -340, -280, -250, -30);
        CompleteStage(school, today.AddDays(-402), lena, "Classroom lighting retrofit concept");
        CompleteStage(school, today.AddDays(-338), lena, "Lighting and power plans");
        CompleteStage(school, today.AddDays(-281), sam, "Construction document set");
        CompleteStage(school, today.AddDays(-252), sam, "Permit approval");
        CompleteStage(school, today.AddDays(-33), avery, "Punch list walkthrough");

        db.AddRange(dana, marcus, avery, sam, priya, tom, lena, ray);
        db.AddRange(cedarValley, ironbridge, summit, harborview, lakeshore);
        db.AddRange(surgicalWing, dataHall, plantUpgrade, towerFitOut, medicalOffice, school);
        await db.SaveChangesAsync(cancellationToken);

        // Panels reference projects by id, so they are added once the projects have keys.
        db.Panels.AddRange(SurgicalWingPanel(surgicalWing.Id), DataHallPanel(dataHall.Id));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>208Y/120 V lighting and receptacle panel, loaded mostly on phase A to show rebalancing.</summary>
    private static ElectricalPanel SurgicalWingPanel(int projectId)
    {
        var panel = ElectricalPanel.Create(
            projectId, "LP-2A", "Level 2 electrical room", VoltageSystem.ThreePhase208Y120, busRatingAmps: 225, mainBreakerAmps: 200, spaces: 42);
        panel.AddCircuit(1, "Lighting, operating rooms 1-2", LoadType.Lighting, 1450m, 1, 20);
        panel.AddCircuit(2, "Lighting, corridor L2", LoadType.Lighting, 980m, 1, 20);
        panel.AddCircuit(3, "Receptacles, OR 1", LoadType.Receptacles, 1440m, 1, 20);
        panel.AddCircuit(5, "Receptacles, OR 2", LoadType.Receptacles, 1440m, 1, 20);
        panel.AddCircuit(7, "Receptacles, nurse station", LoadType.Receptacles, 1260m, 1, 20);
        panel.AddCircuit(8, "Exam lights", LoadType.Lighting, 1100m, 1, 20);
        panel.AddCircuit(13, "Medical gas alarm panel", LoadType.Equipment, 360m, 1, 20);
        panel.AddCircuit(14, "Ceiling exhaust fans", LoadType.Mechanical, 1800m, 1, 20);
        panel.AddCircuit(19, "Sterilizer", LoadType.Equipment, 2600m, 1, 20);
        panel.AddCircuit(20, "Spare", LoadType.Spare, 0m, 1, 20);
        panel.AddCircuit(25, "Water heater WH-2", LoadType.Equipment, 4500m, 2, 30);
        panel.AddCircuit(31, "Exhaust fan EF-3", LoadType.Mechanical, 3600m, 3, 20);
        return panel;
    }

    /// <summary>480Y/277 V distribution panel for the data hall: large three-phase loads.</summary>
    private static ElectricalPanel DataHallPanel(int projectId)
    {
        var panel = ElectricalPanel.Create(
            projectId, "HDP-1", "Electrical room 101", VoltageSystem.ThreePhase480Y277, busRatingAmps: 400, mainBreakerAmps: 400, spaces: 42);
        panel.AddCircuit(1, "CRAH-1", LoadType.Mechanical, 30_000m, 3, 50);
        panel.AddCircuit(2, "CRAH-2", LoadType.Mechanical, 30_000m, 3, 50);
        panel.AddCircuit(7, "UPS-1 input", LoadType.Equipment, 80_000m, 3, 125);
        panel.AddCircuit(8, "Chilled water pump P-1", LoadType.Mechanical, 15_000m, 3, 25);
        panel.AddCircuit(13, "Lighting, data hall", LoadType.Lighting, 4200m, 1, 20);
        panel.AddCircuit(15, "Lighting, corridors", LoadType.Lighting, 2100m, 1, 20);
        return panel;
    }

    /// <summary>Sets stage due dates as day offsets from today, in stage order SD, DD, CD, Permit, CA.</summary>
    private static void Schedule(Project project, DateOnly today, params int[] dueInDays)
    {
        foreach (var (stage, days) in Enum.GetValues<DesignStage>().Zip(dueInDays))
        {
            project.ScheduleMilestone(stage, today.AddDays(days));
        }
    }

    /// <summary>Adds finished tasks to the current stage and closes the stage on the given date.</summary>
    private static void CompleteStage(Project project, DateOnly closedOn, Employee engineer, params string[] taskTitles)
    {
        foreach (var title in taskTitles)
        {
            var task = project.AddTask(title, Discipline.General);
            task.AssignTo(engineer);
            task.MoveTo(ProjectTaskStatus.Done, closedOn);
        }

        project.AdvanceStage(closedOn);
    }

    private static void AddTask(
        Project project,
        DateOnly today,
        string title,
        Discipline discipline,
        Employee? assignee,
        ProjectTaskStatus status,
        TaskPriority priority,
        int dueInDays,
        decimal estimatedHours)
    {
        var task = project.AddTask(title, discipline, priority, today.AddDays(dueInDays), estimatedHours);
        if (assignee is not null)
        {
            task.AssignTo(assignee);
        }

        task.MoveTo(status, today);
    }
}
