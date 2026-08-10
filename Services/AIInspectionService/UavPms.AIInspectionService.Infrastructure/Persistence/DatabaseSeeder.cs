using System.Threading.Tasks;

namespace UavPms.AIInspectionService.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static Task SeedAsync(ApplicationDbContext context)
    {
        return Task.CompletedTask;
    }
}
