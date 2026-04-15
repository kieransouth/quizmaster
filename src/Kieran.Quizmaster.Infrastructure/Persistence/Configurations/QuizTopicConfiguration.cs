using Kieran.Quizmaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kieran.Quizmaster.Infrastructure.Persistence.Configurations;

public class QuizTopicConfiguration : IEntityTypeConfiguration<QuizTopic>
{
    public void Configure(EntityTypeBuilder<QuizTopic> b)
    {
        b.HasKey(t => t.Id);

        b.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(120);

        b.Property(t => t.RequestedCount).IsRequired();

        b.HasIndex(t => t.QuizId);
    }
}
