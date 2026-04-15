using Kieran.Quizmaster.Domain.Entities;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.HasKey(q => q.Id);

        b.Property(q => q.Text).IsRequired();

        b.Property(q => q.Type)
            .HasConversion(
                v => v.Name,
                v => QuestionType.FromName(v, ignoreCase: false))
            .HasMaxLength(32)
            .IsRequired();

        b.Property(q => q.CorrectAnswer).IsRequired();

        b.Property(q => q.OptionsJson);
        b.Property(q => q.Explanation);
        b.Property(q => q.FactCheckNote).HasMaxLength(500);

        // Topic is optional (null for imported questions).
        b.HasOne(q => q.Topic)
            .WithMany()
            .HasForeignKey(q => q.TopicId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(q => q.QuizId);
        b.HasIndex(q => new { q.QuizId, q.Order });
    }
}
