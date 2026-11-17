using Cash.Domain.Interfaces;
using Cash.Infrastructure.Data;
using Cash.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Cash.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connection)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));
        return services.AddScoped<ICashStore, CashStore>();
    }
}