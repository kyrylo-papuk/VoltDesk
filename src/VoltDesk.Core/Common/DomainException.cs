namespace VoltDesk.Core.Common;

/// <summary>A business rule was violated. The message is safe to show to the user.</summary>
public sealed class DomainException(string message) : Exception(message);
