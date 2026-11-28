using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using UavPms.AIInspectionService.API.Controllers;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public class AiRbacAuthorizationAuditTests
{
    private static string[] GetRoles(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();
        var attr = method!.GetCustomAttribute<AuthorizeAttribute>() ?? controllerType.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        return (attr!.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    [Fact]
    public void AIAnalysisController_ShouldIncludeSystemAdminForReadsAndReanalysis_AndExcludeTechnicianFromUpload()
    {
        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.GetById))
            .Should().Contain(UserRoles.SystemAdmin).And.NotContain(UserRoles.Technician);

        // Spec §4: mọi thành viên nhiệm vụ (kể cả Technician) xem defect; handler kiểm tra phân công.
        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.GetMissionAiDetections))
            .Should().Contain(new[] { UserRoles.SystemAdmin, UserRoles.Technician });

        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.AnalyzeExistingMedia))
            .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Manager, UserRoles.Analyst });

        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.UploadStandaloneAnalysis))
            .Should().NotContain(UserRoles.Technician);

        // Spec §4: duyệt defect là Analyst và SystemAdmin.
        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.ReviewMissionAiDetection))
            .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Analyst });
    }

    [Fact]
    public void AnomaliesController_ReviewEndpoints_AllowAnalystAndSystemAdminOnly()
    {
        foreach (var action in new[] { nameof(AnomaliesController.ValidateAnomaly), nameof(AnomaliesController.AssignTower), nameof(AnomaliesController.GetPendingAnomalies) })
        {
            GetRoles(typeof(AnomaliesController), action)
                .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Analyst }, action);
        }
    }
}
