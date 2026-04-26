using Kieran.Quizmaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class UserApiKeyConfiguration : IEntityTypeConfiguration<UserApiKey>
{
    public void Configure(EntityTypeBuilder<UserApiKey> b)
    {
        b.HasKey(k => k.Id);

        b.Property(k => k.Provider)
            .IsRequired()
            .HasMaxLength(64);

        b.Property(k => k.EncryptedKey)
            .IsRequired();

        // One key per (user, provider). Set/clear is upsert semantics.
        b.HasIndex(k => new { k.UserId, k.Provider }).IsUnique();

        b.HasOne(k => k.User)
            .WithMany(u => u.ApiKeys)
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
