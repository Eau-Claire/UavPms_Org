using System.Threading.Tasks;

namespace UavPms.NotificationService.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static Task SeedAsync(ApplicationDbContext context)
    {
        return Task.CompletedTask;
    }
}
