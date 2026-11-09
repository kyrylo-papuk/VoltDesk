using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Options;
using VoltDesk.Core.Projects;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace VoltDesk.Infrastructure.Integrations.Google;

public sealed record DriveItem(string Id, string Url);

public sealed record MilestoneEventData(
    string ProjectNumber,
    string ProjectName,
    DesignStage Stage,
    DateOnly DueDate,
    DateOnly? CompletedOn,
    string? FolderUrl);

public sealed record WorkloadRow(
    string Employee,
    string Role,
    int OpenTasks,
    int OverdueTasks,
    decimal EstimatedHours,
    string Projects);

/// <summary>The Google Workspace operations VoltDesk needs. Faked in tests.</summary>
public interface IGoogleWorkspace
{
    Task<DriveItem> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken);

    /// <summary>Creates or updates an all-day calendar event and returns its id.</summary>
    Task<string> UpsertMilestoneEventAsync(string? eventId, MilestoneEventData milestone, CancellationToken cancellationToken);

    Task<DriveItem> UploadFileAsync(string folderId, string fileName, byte[] content, string mimeType, CancellationToken cancellationToken);

    Task<DriveItem> CreateSpreadsheetAsync(string title, string folderId, IReadOnlyList<IReadOnlyList<object>> rows, CancellationToken cancellationToken);
}

/// <summary>
/// Google Drive, Calendar and Sheets through the official client libraries. Acts as the studio's
/// Google account using a stored OAuth refresh token; Drive access is limited to files the app
/// created (drive.file scope).
/// </summary>
internal sealed class GoogleWorkspace : IGoogleWorkspace
{
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string ApplicationName = "VoltDesk";

    private readonly GoogleOptions _options;
    private readonly Lazy<DriveService> _drive;
    private readonly Lazy<CalendarService> _calendar;
    private readonly Lazy<SheetsService> _sheets;

    public GoogleWorkspace(IOptions<GoogleOptions> options)
    {
        _options = options.Value;
        var initializer = new Lazy<BaseClientService.Initializer>(() => new BaseClientService.Initializer
        {
            HttpClientInitializer = CreateCredential(_options),
            ApplicationName = ApplicationName,
        });
        _drive = new(() => new DriveService(initializer.Value));
        _calendar = new(() => new CalendarService(initializer.Value));
        _sheets = new(() => new SheetsService(initializer.Value));
    }

    public async Task<DriveItem> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken)
    {
        var folder = new DriveFile
        {
            Name = name,
            MimeType = FolderMimeType,
            Parents = parentId is null ? null : [parentId],
        };
        var request = _drive.Value.Files.Create(folder);
        request.Fields = "id, webViewLink";
        var created = await request.ExecuteAsync(cancellationToken);
        return new DriveItem(created.Id, created.WebViewLink);
    }

    public async Task<string> UpsertMilestoneEventAsync(string? eventId, MilestoneEventData milestone, CancellationToken cancellationToken)
    {
        var done = milestone.CompletedOn is not null;
        var calendarEvent = new Event
        {
            Summary = $"{(done ? "✓ " : string.Empty)}{milestone.ProjectNumber} · {milestone.Stage.Abbreviation()} due · {milestone.ProjectName}",
            Description =
                $"{milestone.Stage.DisplayName()} deadline for project {milestone.ProjectNumber} {milestone.ProjectName}." +
                (done ? $"\nClosed on {milestone.CompletedOn:yyyy-MM-dd}." : string.Empty) +
                (milestone.FolderUrl is null ? string.Empty : $"\nProject folder: {milestone.FolderUrl}"),
            Start = new EventDateTime { Date = milestone.DueDate.ToString("yyyy-MM-dd") },
            End = new EventDateTime { Date = milestone.DueDate.AddDays(1).ToString("yyyy-MM-dd") },
            Transparency = "transparent",
        };

        var events = _calendar.Value.Events;
        var saved = eventId is null
            ? await events.Insert(calendarEvent, _options.CalendarId).ExecuteAsync(cancellationToken)
            : await events.Update(calendarEvent, _options.CalendarId, eventId).ExecuteAsync(cancellationToken);
        return saved.Id;
    }

    public async Task<DriveItem> UploadFileAsync(string folderId, string fileName, byte[] content, string mimeType, CancellationToken cancellationToken)
    {
        var metadata = new DriveFile { Name = fileName, Parents = [folderId] };
        using var stream = new MemoryStream(content);
        var upload = _drive.Value.Files.Create(metadata, stream, mimeType);
        upload.Fields = "id, webViewLink";
        var progress = await upload.UploadAsync(cancellationToken);
        if (progress.Exception is not null)
        {
            throw progress.Exception;
        }

        return new DriveItem(upload.ResponseBody.Id, upload.ResponseBody.WebViewLink);
    }

    public async Task<DriveItem> CreateSpreadsheetAsync(
        string title,
        string folderId,
        IReadOnlyList<IReadOnlyList<object>> rows,
        CancellationToken cancellationToken)
    {
        // Created through Drive so it lands in the right folder and stays within the drive.file scope.
        var file = new DriveFile { Name = title, MimeType = "application/vnd.google-apps.spreadsheet", Parents = [folderId] };
        var create = _drive.Value.Files.Create(file);
        create.Fields = "id, webViewLink";
        var spreadsheet = await create.ExecuteAsync(cancellationToken);

        var values = new ValueRange { Values = [.. rows.Select(row => (IList<object>)[.. row])] };
        var update = _sheets.Value.Spreadsheets.Values.Update(values, spreadsheet.Id, "A1");
        update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await update.ExecuteAsync(cancellationToken);

        return new DriveItem(spreadsheet.Id, spreadsheet.WebViewLink);
    }

    private static UserCredential CreateCredential(GoogleOptions options)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = options.ClientId, ClientSecret = options.ClientSecret },
            Scopes = GoogleOptions.Scopes,
        });
        return new UserCredential(flow, "voltdesk", new TokenResponse { RefreshToken = options.RefreshToken });
    }
}

/// <summary>Stands in when the integration is switched off; explains instead of failing obscurely.</summary>
internal sealed class DisabledGoogleWorkspace : IGoogleWorkspace
{
    public Task<DriveItem> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken) => throw Disabled();

    public Task<string> UpsertMilestoneEventAsync(string? eventId, MilestoneEventData milestone, CancellationToken cancellationToken) =>
        throw Disabled();

    public Task<DriveItem> UploadFileAsync(string folderId, string fileName, byte[] content, string mimeType, CancellationToken cancellationToken) =>
        throw Disabled();

    public Task<DriveItem> CreateSpreadsheetAsync(
        string title, string folderId, IReadOnlyList<IReadOnlyList<object>> rows, CancellationToken cancellationToken) => throw Disabled();

    private static IntegrationDisabledException Disabled() => new("Google Workspace");
}

/// <summary>An integration was used while it is not configured on this server.</summary>
public sealed class IntegrationDisabledException(string integration)
    : Exception($"{integration} integration is not configured on this server.");
