using Kieran.Quizmaster.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kieran.Quizmaster.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Quiz>         Quizzes        => Set<Quiz>();
    public DbSet<QuizTopic>    QuizTopics     => Set<QuizTopic>();
    public DbSet<Question>     Questions      => Set<Question>();
    public DbSet<QuizSession>  QuizSessions   => Set<QuizSession>();
    public DbSet<Answer>       Answers        => Set<Answer>();
    public DbSet<RefreshToken> RefreshTokens  => Set<RefreshToken>();
    public DbSet<UserApiKey>   UserApiKeys    => Set<UserApiKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
