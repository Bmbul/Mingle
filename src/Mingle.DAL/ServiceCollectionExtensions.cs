using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mingle.DAL.Seeding;

namespace Mingle.DAL;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the PostgreSQL-backed <see cref="MingleDbContext"/> and the deck seeder.</summary>
    public static IServiceCollection AddMingleDal(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MingleDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());
        services.AddScoped<DeckSeeder>();
        return services;
    }
}
