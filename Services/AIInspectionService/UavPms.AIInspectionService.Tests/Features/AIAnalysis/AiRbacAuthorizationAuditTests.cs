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

        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.GetMissionAiDetections))
            .Should().Contain(UserRoles.SystemAdmin).And.NotContain(UserRoles.Technician);

        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.AnalyzeExistingMedia))
            .Should().BeEquivalentTo(new[] { UserRoles.SystemAdmin, UserRoles.Manager, UserRoles.Analyst });

        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.UploadStandaloneAnalysis))
            .Should().NotContain(UserRoles.Technician);

        GetRoles(typeof(AIAnalysisController), nameof(AIAnalysisController.ReviewMissionAiDetection))
            .Should().BeEquivalentTo(new[] { UserRoles.Analyst });
    }
}
