using VoltDesk.Core.Clients;
using VoltDesk.Core.Common;

namespace VoltDesk.Core.Tests;

public sealed class ClientTests
{
    [Fact]
    public void First_contact_becomes_primary()
    {
        var client = TestData.Client();

        var contact = client.AddContact("Mia Chen", "Facilities Director", "mia@cedarvalley.test", phone: null);

        Assert.True(contact.IsPrimary);
        Assert.Same(contact, client.PrimaryContact);
    }

    [Fact]
    public void Making_another_contact_primary_demotes_the_previous_one()
    {
        var client = TestData.Client();
        var first = client.AddContact("Mia Chen", null, "mia@cedarvalley.test", null);

        var second = client.AddContact("Leo Park", null, "leo@cedarvalley.test", null, makePrimary: true);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Single(client.Contacts, c => c.IsPrimary);
    }

    [Fact]
    public void Removing_the_primary_contact_promotes_the_next_one()
    {
        var client = TestData.Client();
        var first = client.AddContact("Mia Chen", null, "mia@cedarvalley.test", null);
        var second = client.AddContact("Leo Park", null, null, "+1 303 555 0142");

        client.RemoveContact(first);

        Assert.Same(second, client.PrimaryContact);
    }

    [Fact]
    public void Contact_needs_an_email_or_a_phone()
    {
        var client = TestData.Client();

        var error = Assert.Throws<DomainException>(() => client.AddContact("Mia Chen", null, " ", null));

        Assert.Contains("email or a phone", error.Message);
    }

    [Fact]
    public void Contact_emails_are_unique_within_a_client_ignoring_case()
    {
        var client = TestData.Client();
        client.AddContact("Mia Chen", null, "mia@cedarvalley.test", null);

        Assert.Throws<DomainException>(() => client.AddContact("Mia C.", null, "MIA@CedarValley.test", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Client_name_is_required(string? name)
    {
        Assert.Throws<DomainException>(() => Client.Create(name!));
    }

    [Fact]
    public void Invalid_email_is_rejected()
    {
        var client = TestData.Client();

        Assert.Throws<DomainException>(() => client.AddContact("Mia Chen", null, "not-an-email", null));
    }
}
