using VoltDesk.Core.Common;

namespace VoltDesk.Core.Clients;

public sealed class Contact : Entity
{
    public const int FullNameMaxLength = 100;
    public const int TitleMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 32;

    private Contact()
    {
    }

    public int ClientId { get; private set; }

    public string FullName { get; private set; } = null!;

    public string? Title { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsPrimary { get; internal set; }

    internal static Contact Create(string fullName, string? title, string? email, string? phone)
    {
        var contact = new Contact();
        contact.Update(fullName, title, email, phone);
        return contact;
    }

    public void Update(string fullName, string? title, string? email, string? phone)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : Require.Email(email, "Contact email");
        var normalizedPhone = Require.OptionalText(phone, "Contact phone", PhoneMaxLength);
        if (normalizedEmail is null && normalizedPhone is null)
        {
            throw new DomainException("A contact needs an email or a phone number.");
        }

        FullName = Require.Text(fullName, "Contact name", FullNameMaxLength);
        Title = Require.OptionalText(title, "Contact title", TitleMaxLength);
        Email = normalizedEmail;
        Phone = normalizedPhone;
    }
}
