namespace UavPms.AIInspectionService.Domain.Entities;

public class Role
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
