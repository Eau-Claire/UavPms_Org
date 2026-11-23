using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using UavPms.OperationsService.API.Controllers;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Workflows;

/// <summary>
/// Reflection-based RBAC contract audit tests ensuring canonical 5-role invariants and preventing
/// accidental permission regressions across OperationsService controllers.
/// </summary>
public class RbacAuthorizationAuditTests
{
    private static readonly HashSet<string> CanonicalRoles = new(StringComparer.Ordinal)
    {
        UserRoles.SystemAdmin,
        UserRoles.Manager,
        UserRoles.Inspector,
        UserRoles.Analyst,
        UserRoles.Technician
    };

    private static string[] GetRoles(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull($"Action {controllerType.Name}.{actionName} must exist.");

        var attr = method!.GetCustomAttribute<AuthorizeAttribute>()
            ?? controllerType.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull($"Action {controllerType.Name}.{actionName} must have [Authorize].");

        return (attr!.Roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    [Fact]
    public void UserRolesConstants_ShouldBeCanonicalAndAccurate()
    {
        var adminManagerInspectorAnalyst = UserRoles.AdminManagerInspectorAnalyst.Split(',');
        adminManagerInspectorAnalyst.Should().BeEquivalentTo(
            new[] { UserRoles.SystemAdmin, UserRoles.Manager, UserRoles.Inspector, UserRoles.Analyst });
        adminManagerInspectorAnalyst.Should().NotContain(UserRoles.Technician);

        var adminManagerTechnician = UserRoles.AdminManagerTechnician.Split(',');
        adminManagerTechnician.Should().BeEquivalentTo(
            new[] { UserRoles.SystemAdmin, UserRoles.Manager, UserRoles.Technician });
    }

    [Fact]
    public void AllOperationsControllers_AuthorizeAttributes_ShouldOnlyContainUniqueCanonicalRoles()
    {
        var controllerTypes = typeof(MissionController).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && t.Name.EndsWith("Controller", StringComparison.Ordinal));

        foreach (var controller in controllerTypes)
        {
            var attrs = controller.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>()));

            foreach (var attr in attrs)
            {
                if (string.IsNullOrWhiteSpace(attr.Roles)) continue;

                var roles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                roles.Should().OnlyHaveUniqueItems($"Duplicate role found on {controller.Name}: '{attr.Roles}'");
                roles.Should().AllSatisfy(r =>
                    CanonicalRoles.Should().Contain(r, $"Non-canonical role '{r}' found on {controller.Name}"));
            }
        }
    }

    [Theory]
    [InlineData(nameof(PreMissionAssessmentV2Controller.Create))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.Evaluate))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.ReEvaluate))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.CreateMission))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.MarkCompleted))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.Consume))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.Cancel))]
    [InlineData(nameof(PreMissionAssessmentV2Controller.Confirm))]
    public void PreMissionAssessmentMutations_ShouldBeRestrictedToAdminAndManager(string actionName)
    {
        var roles = GetRoles(typeof(PreMissionAssessmentV2Controller), actionName);
        roles.Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Manager });
    }

    [Fact]
    public void ReportController_CreateAndSubmit_ShouldExcludeTechnician()
    {
        GetRoles(typeof(ReportController), nameof(ReportController.Create)).Should().NotContain(UserRoles.Technician);
        GetRoles(typeof(ReportController), nameof(ReportController.Submit)).Should().NotContain(UserRoles.Technician);
        GetRoles(typeof(ReportController), nameof(ReportController.Approve))
            .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Manager });
    }

    [Fact]
    public void AlertsAndMaintenanceControllers_ShouldEnforceMf04Roles()
    {
        GetRoles(typeof(AlertsController), nameof(AlertsController.ReviewAlert))
            .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Manager, UserRoles.Analyst });

        GetRoles(typeof(AlertsController), nameof(AlertsController.TriggerAlert))
            .Should().NotContain(UserRoles.Technician);

        GetRoles(typeof(MaintenanceTicketController), nameof(MaintenanceTicketController.UpdateStatus))
            .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Manager, UserRoles.Technician });

        GetRoles(typeof(MaintenanceTicketController), nameof(MaintenanceTicketController.GetMyTickets))
            .Should().BeEquivalentTo(new[] { UserRoles.Technician });
    }
}
