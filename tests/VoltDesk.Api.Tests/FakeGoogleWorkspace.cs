using System.Collections.Concurrent;
using VoltDesk.Infrastructure.Integrations.Google;

namespace VoltDesk.Api.Tests;

/// <summary>In-memory Google Workspace. Thread-safe because test classes run in parallel.</summary>
public sealed class FakeGoogleWorkspace : IGoogleWorkspace
{
    private int _nextId;

    public ConcurrentDictionary<string, (string Name, string? ParentId)> Folders { get; } = new();

    public ConcurrentDictionary<string, MilestoneEventData> Events { get; } = new();

    public ConcurrentDictionary<string, int> EventWrites { get; } = new();

    public ConcurrentDictionary<string, (string FolderId, string FileName, int Size)> Uploads { get; } = new();

    public ConcurrentDictionary<string, (string Title, string FolderId, IReadOnlyList<IReadOnlyList<object>> Rows)> Spreadsheets { get; } = new();

    /// <summary>Folder names starting with these prefixes fail, to simulate a Google outage for one project.</summary>
    public ConcurrentDictionary<string, bool> FailingFolderPrefixes { get; } = new();

    public Task<DriveItem> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken)
    {
        if (FailingFolderPrefixes.Keys.Any(name.StartsWith))
        {
            throw new HttpRequestException("Simulated Google Drive outage.");
        }

        var id = NewId("folder");
        Folders[id] = (name, parentId);
        return Task.FromResult(new DriveItem(id, $"https://drive.example/folders/{id}"));
    }

    public Task<string> UpsertMilestoneEventAsync(string? eventId, MilestoneEventData milestone, CancellationToken cancellationToken)
    {
        var id = eventId ?? NewId("event");
        Events[id] = milestone;
        EventWrites.AddOrUpdate(id, 1, (_, writes) => writes + 1);
        return Task.FromResult(id);
    }

    public Task<DriveItem> UploadFileAsync(string folderId, string fileName, byte[] content, string mimeType, CancellationToken cancellationToken)
    {
        var id = NewId("file");
        Uploads[id] = (folderId, fileName, content.Length);
        return Task.FromResult(new DriveItem(id, $"https://drive.example/files/{id}"));
    }

    public Task<DriveItem> CreateSpreadsheetAsync(
        string title, string folderId, IReadOnlyList<IReadOnlyList<object>> rows, CancellationToken cancellationToken)
    {
        var id = NewId("sheet");
        Spreadsheets[id] = (title, folderId, rows);
        return Task.FromResult(new DriveItem(id, $"https://sheets.example/{id}"));
    }

    private string NewId(string prefix) => $"{prefix}-{Interlocked.Increment(ref _nextId)}";
}
