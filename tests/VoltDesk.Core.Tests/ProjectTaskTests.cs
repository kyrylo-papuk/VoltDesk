using VoltDesk.Core.Common;
using VoltDesk.Core.Projects;

namespace VoltDesk.Core.Tests;

public sealed class ProjectTaskTests
{
    [Fact]
    public void New_task_starts_in_to_do_within_the_current_stage()
    {
        var project = TestData.Project();
        project.AdvanceStage(TestData.Today);

        var task = project.AddTask("Voltage drop calculations", Discipline.Power, TaskPriority.High);

        Assert.Equal(ProjectTaskStatus.ToDo, task.Status);
        Assert.Equal(DesignStage.DesignDevelopment, task.Stage);
        Assert.Equal(TaskPriority.High, task.Priority);
    }

    [Theory]
    [InlineData(ProjectTaskStatus.InProgress)]
    [InlineData(ProjectTaskStatus.InReview)]
    [InlineData(ProjectTaskStatus.Done)]
    public void Unassigned_task_cannot_leave_to_do(ProjectTaskStatus status)
    {
        var task = TestData.Project().AddTask("Fire alarm riser", Discipline.FireAlarm);

        Assert.Throws<DomainException>(() => task.MoveTo(status, TestData.Today));
        Assert.Equal(ProjectTaskStatus.ToDo, task.Status);
    }

    [Fact]
    public void Done_records_the_completion_date_and_reopening_clears_it()
    {
        var task = TestData.AddDoneTask(TestData.Project());
        Assert.Equal(TestData.Today, task.CompletedOn);

        task.MoveTo(ProjectTaskStatus.InProgress, TestData.Today);

        Assert.Null(task.CompletedOn);
    }

    [Fact]
    public void Inactive_employee_cannot_be_assigned()
    {
        var task = TestData.Project().AddTask("Generator sizing", Discipline.Power);
        var engineer = TestData.Engineer();
        engineer.Deactivate();

        Assert.Throws<DomainException>(() => task.AssignTo(engineer));
    }

    [Fact]
    public void Task_in_progress_cannot_be_unassigned()
    {
        var task = TestData.Project().AddTask("Lighting controls narrative", Discipline.Lighting);
        task.AssignTo(TestData.Engineer());
        task.MoveTo(ProjectTaskStatus.InProgress, TestData.Today);

        Assert.Throws<DomainException>(task.Unassign);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(1001)]
    public void Estimate_must_be_positive_and_realistic(int hours)
    {
        var project = TestData.Project();

        Assert.Throws<DomainException>(
            () => project.AddTask("Short-circuit study", Discipline.Power, estimatedHours: hours));
    }

    [Fact]
    public void Undefined_enum_values_are_rejected()
    {
        var project = TestData.Project();

        Assert.Throws<DomainException>(() => project.AddTask("Unknown", (Discipline)42));
    }
}
