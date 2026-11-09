using System.ComponentModel.DataAnnotations;
using VoltDesk.Core.Clients;

namespace VoltDesk.Contracts;

public sealed record ClientSummaryDto(
    int Id,
    string Name,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    int ActiveProjects);

public sealed record ClientDto(int Id, string Name, IReadOnlyList<ContactDto> Contacts, string Version);

public sealed record ContactDto(int Id, string FullName, string? Title, string? Email, string? Phone, bool IsPrimary);

public sealed record CreateClientRequest([Required, StringLength(Client.NameMaxLength)] string Name);

public sealed record RenameClientRequest(
    [Required, StringLength(Client.NameMaxLength)] string Name,
    [Required] string Version);

/// <summary>Adds or edits a contact. <see cref="Version"/> is the client's version.</summary>
public sealed record ContactRequest(
    [Required, StringLength(Contact.FullNameMaxLength)] string FullName,
    [StringLength(Contact.TitleMaxLength)] string? Title,
    [EmailAddress, StringLength(Contact.EmailMaxLength)] string? Email,
    [StringLength(Contact.PhoneMaxLength)] string? Phone,
    bool MakePrimary,
    [Required] string Version);
