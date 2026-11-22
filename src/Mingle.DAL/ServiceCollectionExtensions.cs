using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mingle.DAL.Seeding;

namespace Mingle.DAL;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the PostgreSQL-backed <see cref="MingleDbContext"/> and the deck seeder.</summary>
    public static IServiceCollection AddMingleDal(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MingleDbContext>(options => UsePostgres(options, connectionString));
        services.AddScoped<DeckSeeder>();
        services.AddScoped<IDeckSource, DbDeckSource>();
        return services;
    }

    /// <summary>The one place the database provider and naming conventions are chosen (also used by tests).</summary>
    public static DbContextOptionsBuilder UsePostgres(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
