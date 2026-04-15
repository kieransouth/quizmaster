using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.HasKey(q => q.Id);

        b.Property(q => q.Title)
            .IsRequired()
            .HasMaxLength(200);

        b.Property(q => q.Source)
            .HasConversion(
                v => v.Name,
                v => QuizSource.FromName(v, ignoreCase: false))
            .HasMaxLength(32)
            .IsRequired();

        b.Property(q => q.SourceText); // unbounded text — pasted quizzes can be long

        b.Property(q => q.ProviderUsed)
            .IsRequired()
            .HasMaxLength(64);

        b.Property(q => q.ModelUsed)
            .IsRequired()
            .HasMaxLength(128);

        b.Property(q => q.CreatedAt).IsRequired();

        b.HasOne(q => q.CreatedByUser)
            .WithMany(u => u.Quizzes)
            .HasForeignKey(q => q.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict); // don't lose quizzes when a user is deleted

        b.HasMany(q => q.Topics)
            .WithOne(t => t.Quiz!)
            .HasForeignKey(t => t.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(q => q.Questions)
            .WithOne(qu => qu.Quiz!)
            .HasForeignKey(qu => qu.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(q => q.CreatedByUserId);
    }
}
