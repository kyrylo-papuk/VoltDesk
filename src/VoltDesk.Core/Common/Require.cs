using System.Net.Mail;

namespace VoltDesk.Core.Common;

internal static class Require
{
    public static string Text(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} is required.");
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new DomainException($"{field} must be at most {maxLength} characters.");
    }

    public static string? OptionalText(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : Text(value, field, maxLength);

    public static string Email(string? value, string field)
    {
        var email = Text(value, field, 254).ToLowerInvariant();
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email
            ? email
            : throw new DomainException($"{field} '{value}' is not a valid email address.");
    }

    public static TEnum Defined<TEnum>(TEnum value, string field)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException($"{field} '{value}' is not supported.");
}
