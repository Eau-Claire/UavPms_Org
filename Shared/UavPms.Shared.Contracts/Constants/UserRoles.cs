namespace UavPms.Shared.Contracts.Constants;

public static class UserRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string Manager = "Manager";
    public const string Inspector = "Inspector";
    public const string Analyst = "Analyst";
    public const string MaintenanceTechnician = "MaintenanceTechnician";

    public const string AdminOnly = SystemAdmin;
    public const string AdminAndManager = "SystemAdmin,Manager";
    public const string AdminManagerAnalyst = "SystemAdmin,Manager,Analyst";
    public const string AdminManagerInspector = "SystemAdmin,Manager,Inspector";
    public const string AdminManagerInspectorAnalyst = "SystemAdmin,Manager,Inspector,Analyst";
    public const string InspectorOnly = Inspector;
    public const string ManagerAndInspector = "Manager,Inspector";
    public const string AllAuthenticatedRoles = "SystemAdmin,Manager,Inspector,Analyst,MaintenanceTechnician";
}