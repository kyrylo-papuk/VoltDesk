namespace VoltDesk.Contracts;

public sealed record GoogleStatusDto(bool Enabled, int PendingMessages, int FailedMessages);

/// <summary>A file VoltDesk created in Google Drive.</summary>
public sealed record ExternalFileDto(string Name, string Url);
