using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Seed Roles
        var defaultRoles = new List<string> { "SystemAdmin", "Manager", "Inspector", "Analyst", "Technician" };
        var existingRoles = await context.Roles.ToListAsync();
        var rolesToCreate = defaultRoles.Where(r => !existingRoles.Any(er => er.RoleName == r)).ToList();

        foreach (var roleName in rolesToCreate)
        {
            context.Roles.Add(new Role
            {
                RoleName = roleName,
                Description = $"Default role for {roleName}"
            });
        }

        if (rolesToCreate.Any())
        {
            await context.SaveChangesAsync();
        }

        var targetUserId = Guid.Parse("469bfac4-8b96-4f27-a772-945cff2fbaa8");
        var targetUser = await context.Users.FindAsync(targetUserId);
        if (targetUser == null)
        {
            targetUser = new User
            {
                Id = targetUserId,
                Username = "uav_operator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator123!", 10),
                FullName = "UAV Operator",
                Email = "operator@uavpms.com",
                Phone = "0987654321",
                Status = "Active"
            };
            context.Users.Add(targetUser);
            await context.SaveChangesAsync();

            var inspectorRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Inspector");
            if (inspectorRole != null)
            {
                context.UserRoles.Add(new UserRole
                {
                    UserId = targetUserId,
                    RoleId = inspectorRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
        }

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ""Regions"" (""Id"", ""RegionName"", ""IsDeleted"", ""CreatedAt"")
            VALUES ('00000000-0000-0000-0000-000000000000', 'Default Region', false, now())
            ON CONFLICT (""Id"") DO NOTHING;
            
            INSERT INTO ""Substations"" (""Id"", ""RegionAssetId"", ""SubstationName"", ""VoltageLevel"", ""IsDeleted"", ""CreatedAt"")
            VALUES ('00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000', 'Default Substation', '220kV', false, now())
            ON CONFLICT (""Id"") DO NOTHING;

            INSERT INTO ""TransmissionLines"" (""Id"", ""SubstationAssetId"", ""LineName"", ""IsCriticalEdge"", ""IsDeleted"", ""CreatedAt"")
            VALUES ('00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000', 'Default Transmission Line', false, false, now())
            ON CONFLICT (""Id"") DO NOTHING;

            INSERT INTO ""Towers"" (""Id"", ""LineAssetId"", ""TowerCode"", ""IsDeleted"", ""CreatedAt"")
            VALUES ('00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000', 'TOWER-DEFAULT', false, now())
            ON CONFLICT (""Id"") DO NOTHING;

            INSERT INTO ""Assets"" (""Id"", ""TowerId"", ""AssetType"", ""AssetCode"", ""Status"", ""CurrentHealthScore"", ""RiskLevel"", ""IsDeleted"", ""CreatedAt"")
            VALUES ('00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000', 'Default Asset', 'ASSET-DEFAULT', 'Active', 100.0, 'Low', false, now())
            ON CONFLICT (""Id"") DO NOTHING;
        ");


        var testUavId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var uav = await context.Uavs.FindAsync(testUavId);
        if (uav == null)
        {
            uav = new Uav
            {
                Id = testUavId,
                UavCode = "UAV001",
                Model = "DJI Matrice 300 RTK",
                Status = "Online",
                BatteryLevel = 95.0
            };
            context.Uavs.Add(uav);
            await context.SaveChangesAsync();
        }

        var testDefectCatId = 1;
        var defectCat = await context.DefectCategories.FindAsync(testDefectCatId);
        if (defectCat == null)
        {
            defectCat = new DefectCategory
            {
                Id = testDefectCatId,
                CategoryCode = "Corrosion",
                CategoryName = "Corrosion",
                SeverityWeight = 3,
                IsEmergencyClass = false
            };
            context.DefectCategories.Add(defectCat);
            await context.SaveChangesAsync();
        }

        var testMissionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var mission = await context.Missions.FindAsync(testMissionId);
        if (mission == null)
        {
            mission = new Mission
            {
                Id = testMissionId,
                MissionCode = "MS-0001",
                ManagerId = targetUserId,
                InspectorId = targetUserId,
                UavId = testUavId,
                Status = "Executing",
                ScheduledStartAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
                Description = "Autonomous inspection mission"
            };
            context.Missions.Add(mission);
            await context.SaveChangesAsync();
        }


        // 2. Seed default Admin User
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "SystemAdmin");
        if (adminRole != null)
        {
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            if (adminUser == null)
            {
                var adminPassword = Environment.GetEnvironmentVariable("UAVPMS_ADMIN_PASSWORD");
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    return;
                }
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, 10);

                var newAdmin = new User
                {
                    Username = "admin",
                    PasswordHash = passwordHash,
                    FullName = "System Administrator",
                    Email = "admin@uavpms.com",
                    Phone = "0123456789",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(newAdmin);
                await context.SaveChangesAsync();

                // Assign role
                context.UserRoles.Add(new UserRole
                {
                    UserId = newAdmin.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
