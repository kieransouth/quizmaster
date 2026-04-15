using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class QuizSessionConfiguration : IEntityTypeConfiguration<QuizSession>
{
    public void Configure(EntityTypeBuilder<QuizSession> b)
    {
        b.HasKey(s => s.Id);

        b.Property(s => s.StartedAt).IsRequired();

        b.Property(s => s.Status)
            .HasConversion(
                v => v.Name,
                v => SessionStatus.FromName(v, ignoreCase: false))
            .HasMaxLength(32)
            .IsRequired();

        b.Property(s => s.PublicShareToken)
            .IsRequired()
            .HasMaxLength(64);

        // Lookup path for /share/:token
        b.HasIndex(s => s.PublicShareToken).IsUnique();

        b.HasOne(s => s.Quiz)
            .WithMany()
            .HasForeignKey(s => s.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(s => s.HostUser)
            .WithMany()
            .HasForeignKey(s => s.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(s => s.Answers)
            .WithOne(a => a.QuizSession!)
            .HasForeignKey(a => a.QuizSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(s => s.HostUserId);
    }
}
