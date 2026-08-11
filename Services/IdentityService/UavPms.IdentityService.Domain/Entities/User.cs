using UavPms.IdentityService.Domain.Common;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Domain.Entities;

public class User : BaseEntity
{
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;

    public bool IsEmailVerified { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    
    #region Rich Domain Methods
    public void VerifyEmail()
    {
        IsEmailVerified = true;
        Status = UserStatus.Active;
    }

    public void Activate()
    {
        Status = UserStatus.Active;
    }

    public void Suspend()
    {
        Status = UserStatus.Suspended;
    }

    public void Deactivate()
    {
        Status = UserStatus.Inactive;
    }
    
    public bool IsActive() => Status == UserStatus.Active;
    #endregion
}
