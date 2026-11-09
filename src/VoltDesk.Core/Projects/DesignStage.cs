namespace VoltDesk.Core.Projects;

/// <summary>Design phases of a US building project, in the order they happen.</summary>
public enum DesignStage
{
    SchematicDesign = 1,
    DesignDevelopment = 2,
    ConstructionDocuments = 3,
    Permitting = 4,
    ConstructionAdministration = 5,
}

public static class DesignStageExtensions
{
    public static string Abbreviation(this DesignStage stage) => stage switch
    {
        DesignStage.SchematicDesign => "SD",
        DesignStage.DesignDevelopment => "DD",
        DesignStage.ConstructionDocuments => "CD",
        DesignStage.Permitting => "Permit",
        DesignStage.ConstructionAdministration => "CA",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };

    public static string DisplayName(this DesignStage stage) => stage switch
    {
        DesignStage.SchematicDesign => "Schematic Design",
        DesignStage.DesignDevelopment => "Design Development",
        DesignStage.ConstructionDocuments => "Construction Documents",
        DesignStage.Permitting => "Permitting",
        DesignStage.ConstructionAdministration => "Construction Administration",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };
}
