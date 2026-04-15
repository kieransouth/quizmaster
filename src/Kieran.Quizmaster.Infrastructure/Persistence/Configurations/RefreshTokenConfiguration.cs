using Kieran.Quizmaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(t => t.Id);

        b.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(128); // SHA-256 hex/base64 fits comfortably

        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasIndex(t => t.UserId);

        b.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
