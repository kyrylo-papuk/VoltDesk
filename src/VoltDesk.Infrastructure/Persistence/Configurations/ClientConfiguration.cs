using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Clients;

namespace VoltDesk.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasTrackingColumns();
        builder.Property(c => c.Name).HasMaxLength(Client.NameMaxLength);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasMany(c => c.Contacts)
            .WithOne()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.PrimaryContact);
    }
}

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.Property(c => c.FullName).HasMaxLength(Contact.FullNameMaxLength);
        builder.Property(c => c.Title).HasMaxLength(Contact.TitleMaxLength);
        builder.Property(c => c.Email).HasMaxLength(Contact.EmailMaxLength);
        builder.Property(c => c.Phone).HasMaxLength(Contact.PhoneMaxLength);

        // Mirrors the domain rule: one contact per email within a client.
        builder.HasIndex(c => new { c.ClientId, c.Email })
            .IsUnique()
            .HasFilter("[Email] IS NOT NULL");
    }
}
