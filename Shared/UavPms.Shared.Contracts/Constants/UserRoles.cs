namespace UavPms.Shared.Contracts.Constants;

public static class UserRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string Manager = "Manager";
    public const string Inspector = "Inspector";
    public const string Analyst = "Analyst";
    public const string Technician = "Technician";

    public const string AdminOnly = SystemAdmin;
    public const string AdminAndManager = "SystemAdmin,Manager";
    public const string AdminManagerAnalyst = "SystemAdmin,Manager,Analyst";
    public const string AdminManagerInspector = "SystemAdmin,Manager,Inspector";
    public const string AdminManagerTechnician = "SystemAdmin,Manager,Technician";
    // BE-REQ-101: chỉ Technician (và SystemAdmin) được ghi BIST/FC; Manager/Inspector chỉ đọc, Analyst bị loại.
    public const string AdminTechnician = "SystemAdmin,Technician";
    public const string AdminManagerInspectorTechnician = "SystemAdmin,Manager,Inspector,Technician";
    public const string AdminManagerInspectorAnalyst = "SystemAdmin,Manager,Inspector,Analyst";
    public const string InspectorOnly = Inspector;
    public const string ManagerAndInspector = "Manager,Inspector";
    public const string AllAuthenticatedRoles = "SystemAdmin,Manager,Inspector,Analyst,Technician";
}