using VoltDesk.Core.Common;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;

namespace VoltDesk.Core.Tests;

public sealed class ProjectTests
{
    [Fact]
    public void New_project_is_active_in_schematic_design_with_a_milestone_per_stage()
    {
        var project = TestData.Project();

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(DesignStage.SchematicDesign, project.Stage);
        Assert.Equal(Enum.GetValues<DesignStage>(), project.Milestones.Select(m => m.Stage));
        Assert.All(project.Milestones, m => Assert.Null(m.CompletedOn));
    }

    [Fact]
    public void Number_and_state_are_normalized()
    {
        var project = Project.Create(
            " 2026-dc-02 ", "Data Hall 2", TestData.Client(), BuildingType.DataCenter, "Dallas", "tx", TestData.Manager(), 0m);

        Assert.Equal("2026-DC-02", project.Number);
        Assert.Equal("TX", project.State);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("Colorado")]
    [InlineData("")]
    public void State_must_be_a_us_state_code(string state)
    {
        Assert.Throws<DomainException>(() => Project.Create(
            "2026-001", "Clinic", TestData.Client(), BuildingType.Healthcare, "Denver", state, TestData.Manager(), 0m));
    }

    [Fact]
    public void Engineer_cannot_be_the_project_manager()
    {
        var error = Assert.Throws<DomainException>(() => Project.Create(
            "2026-001", "Clinic", TestData.Client(), BuildingType.Healthcare, "Denver", "CO", TestData.Engineer(), 0m));

        Assert.Contains("cannot manage projects", error.Message);
    }

    [Fact]
    public void Inactive_manager_cannot_be_assigned()
    {
        var project = TestData.Project();
        var manager = Employee.Create("Ruth Gale", "ruth@voltdesk.test", EmployeeRole.ProjectManager);
        manager.Deactivate();

        Assert.Throws<DomainException>(() => project.AssignManager(manager));
    }

    [Fact]
    public void Advancing_closes_the_current_milestone_and_moves_to_the_next_stage()
    {
        var project = TestData.Project();

        project.AdvanceStage(TestData.Today);

        Assert.Equal(DesignStage.DesignDevelopment, project.Stage);
        Assert.Equal(TestData.Today, project.Milestone(DesignStage.SchematicDesign).CompletedOn);
    }

    [Fact]
    public void Stage_with_open_tasks_cannot_be_closed()
    {
        var project = TestData.Project();
        TestData.AddDoneTask(project);
        project.AddTask("Lighting layout, level 1", Discipline.Lighting);

        var error = Assert.Throws<DomainException>(() => project.AdvanceStage(TestData.Today));

        Assert.Contains("1 open task", error.Message);
        Assert.Equal(DesignStage.SchematicDesign, project.Stage);
    }

    [Fact]
    public void Open_tasks_of_an_earlier_stage_do_not_block_later_stages()
    {
        var project = TestData.Project();
        var task = TestData.AddDoneTask(project);
        project.AdvanceStage(TestData.Today);

        task.MoveTo(ProjectTaskStatus.InReview, TestData.Today);
        project.AdvanceStage(TestData.Today);

        Assert.Equal(DesignStage.ConstructionDocuments, project.Stage);
    }

    [Fact]
    public void Closing_construction_administration_completes_the_project()
    {
        var project = TestData.Project();

        foreach (var _ in Enum.GetValues<DesignStage>())
        {
            project.AdvanceStage(TestData.Today);
        }

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(DesignStage.ConstructionAdministration, project.Stage);
        Assert.All(project.Milestones, m => Assert.Equal(TestData.Today, m.CompletedOn));
        Assert.Throws<DomainException>(() => project.AdvanceStage(TestData.Today));
    }

    [Fact]
    public void Project_on_hold_cannot_advance_or_get_tasks_until_resumed()
    {
        var project = TestData.Project();
        project.PutOnHold();

        Assert.Throws<DomainException>(() => project.AdvanceStage(TestData.Today));
        Assert.Throws<DomainException>(() => project.AddTask("Panel schedules", Discipline.Power));

        project.Resume();
        project.AdvanceStage(TestData.Today);
        Assert.Equal(DesignStage.DesignDevelopment, project.Stage);
    }

    [Fact]
    public void Cancelled_project_cannot_be_rescheduled()
    {
        var project = TestData.Project();
        project.Cancel();

        Assert.Throws<DomainException>(() => project.ScheduleMilestone(DesignStage.DesignDevelopment, TestData.Today));
    }

    [Fact]
    public void Milestones_can_be_scheduled_in_stage_order()
    {
        var project = TestData.Project();

        project.ScheduleMilestone(DesignStage.SchematicDesign, new DateOnly(2026, 12, 1));
        project.ScheduleMilestone(DesignStage.ConstructionDocuments, new DateOnly(2027, 3, 1));
        project.ScheduleMilestone(DesignStage.DesignDevelopment, new DateOnly(2027, 1, 15));

        Assert.Equal(new DateOnly(2027, 1, 15), project.Milestone(DesignStage.DesignDevelopment).DueDate);
    }

    [Fact]
    public void Milestone_cannot_be_due_before_an_earlier_stage()
    {
        var project = TestData.Project();
        project.ScheduleMilestone(DesignStage.DesignDevelopment, new DateOnly(2027, 2, 1));

        var error = Assert.Throws<DomainException>(
            () => project.ScheduleMilestone(DesignStage.ConstructionDocuments, new DateOnly(2027, 1, 1)));

        Assert.Contains("before Design Development", error.Message);
    }

    [Fact]
    public void Milestone_cannot_be_due_after_a_later_stage()
    {
        var project = TestData.Project();
        project.ScheduleMilestone(DesignStage.Permitting, new DateOnly(2027, 5, 1));

        Assert.Throws<DomainException>(
            () => project.ScheduleMilestone(DesignStage.SchematicDesign, new DateOnly(2027, 6, 1)));
    }

    [Fact]
    public void Completed_milestone_cannot_be_rescheduled()
    {
        var project = TestData.Project();
        project.AdvanceStage(TestData.Today);

        Assert.Throws<DomainException>(
            () => project.ScheduleMilestone(DesignStage.SchematicDesign, TestData.Today));
    }

    [Fact]
    public void Milestone_is_overdue_only_when_open_and_past_due()
    {
        var project = TestData.Project();
        project.ScheduleMilestone(DesignStage.SchematicDesign, TestData.Today.AddDays(-1));
        var milestone = project.Milestone(DesignStage.SchematicDesign);

        Assert.True(milestone.IsOverdue(TestData.Today));
        Assert.False(project.Milestone(DesignStage.DesignDevelopment).IsOverdue(TestData.Today));

        project.AdvanceStage(TestData.Today);
        Assert.False(milestone.IsOverdue(TestData.Today));
    }

    [Fact]
    public void Negative_fee_is_rejected()
    {
        var project = TestData.Project();

        Assert.Throws<DomainException>(
            () => project.UpdateDetails(project.Name, project.BuildingType, project.City, project.State, -1m));
    }
}
