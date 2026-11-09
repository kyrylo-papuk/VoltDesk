using VoltDesk.Core.Common;

namespace VoltDesk.Core.Clients;

public sealed class Client : Entity
{
    public const int NameMaxLength = 200;

    private readonly List<Contact> _contacts = [];

    private Client()
    {
    }

    public string Name { get; private set; } = null!;

    public IReadOnlyList<Contact> Contacts => _contacts;

    public Contact? PrimaryContact => _contacts.FirstOrDefault(c => c.IsPrimary);

    public static Client Create(string name) => new() { Name = Require.Text(name, "Client name", NameMaxLength) };

    public void Rename(string name) => Name = Require.Text(name, "Client name", NameMaxLength);

    /// <summary>Adds a contact. The first contact always becomes the primary one.</summary>
    public Contact AddContact(string fullName, string? title, string? email, string? phone, bool makePrimary = false)
    {
        var contact = Contact.Create(fullName, title, email, phone);
        if (contact.Email is not null && _contacts.Any(c => c.Email == contact.Email))
        {
            throw new DomainException($"{Name} already has a contact with email {contact.Email}.");
        }

        _contacts.Add(contact);
        if (makePrimary || _contacts.Count == 1)
        {
            SetPrimaryContact(contact);
        }

        return contact;
    }

    public void SetPrimaryContact(Contact contact)
    {
        EnsureOwns(contact);
        foreach (var c in _contacts)
        {
            c.IsPrimary = ReferenceEquals(c, contact);
        }
    }

    /// <summary>Removes a contact. If it was the primary one, the next contact takes its place.</summary>
    public void RemoveContact(Contact contact)
    {
        EnsureOwns(contact);
        _contacts.Remove(contact);
        if (contact.IsPrimary && _contacts.Count > 0)
        {
            SetPrimaryContact(_contacts[0]);
        }
    }

    private void EnsureOwns(Contact contact)
    {
        if (!_contacts.Contains(contact))
        {
            throw new DomainException($"The contact does not belong to {Name}.");
        }
    }
}
