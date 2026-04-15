using Kieran.Quizmaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> b)
    {
        b.HasKey(a => a.Id);

        b.Property(a => a.AnswerText)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        // 0.0–1.0; precision (3,2) gives headroom without going to numeric default.
        b.Property(a => a.PointsAwarded)
            .HasPrecision(3, 2)
            .IsRequired();

        b.Property(a => a.GradingNote).HasMaxLength(500);

        b.HasOne(a => a.Question)
            .WithMany()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // One team answer per question per session.
        b.HasIndex(a => new { a.QuizSessionId, a.QuestionId }).IsUnique();
    }
}
