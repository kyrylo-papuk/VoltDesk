using Google.Apis.Calendar.v3;
using Google.Apis.Drive.v3;

namespace VoltDesk.Infrastructure.Integrations.Google;

public sealed class GoogleOptions
{
    public const string Section = "Google";

    /// <summary>Least privilege: Drive files the app created, and calendar events.</summary>
    public static readonly IReadOnlyList<string> Scopes = [DriveService.Scope.DriveFile, CalendarService.Scope.CalendarEvents];

    /// <summary>Off by default: the app works fully without Google.</summary>
    public bool Enabled { get; set; }

    /// <summary>Runs the background sync worker. Tests turn it off and process the outbox directly.</summary>
    public bool RunWorker { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Refresh token of the studio account; get one with tools/VoltDesk.GoogleAuth.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public string CalendarId { get; set; } = "primary";

    /// <summary>Folder created in Drive to hold all project folders.</summary>
    public string RootFolderName { get; set; } = "VoltDesk projects";

    /// <summary>Subfolders of every project folder, in the order engineers expect.</summary>
    public List<string> ProjectSubfolders { get; set; } =
    [
        "01 Schematic Design",
        "02 Design Development",
        "03 Construction Documents",
        "04 Permitting",
        "05 Construction Administration",
        "Calculations",
        "Panel schedules",
        "Correspondence",
    ];
}
