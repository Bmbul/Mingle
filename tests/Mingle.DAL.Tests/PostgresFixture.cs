using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mingle.DAL.Tests;

/// <summary>
/// One real PostgreSQL container for all database tests (needs Docker). Each test gets its own
/// empty database in it, so tests never see each other's data.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => container.StartAsync();

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    /// <summary>A context on a brand-new database with all migrations applied.</summary>
    public async Task<MingleDbContext> NewDatabaseAsync()
    {
        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"test_{Guid.NewGuid():N}",
        }.ConnectionString;

        var options = ServiceCollectionExtensions.UsePostgres(new DbContextOptionsBuilder<MingleDbContext>(), connectionString);
        var db = new MingleDbContext((DbContextOptions<MingleDbContext>)options.Options);
        await db.Database.MigrateAsync();
        return db;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
