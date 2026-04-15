using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kieran.Quizmaster.Tests.TestSupport;

/// <summary>
/// Spins up a SQLite in-memory database with our schema applied via
/// EnsureCreated. Disposing closes the connection (drops the DB).
/// </summary>
internal sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Db { get; }

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new ApplicationDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
