using System.Threading.Tasks;

namespace UavPms.OperationsService.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static Task SeedAsync(ApplicationDbContext context)
    {
        return Task.CompletedTask;
    }
}
