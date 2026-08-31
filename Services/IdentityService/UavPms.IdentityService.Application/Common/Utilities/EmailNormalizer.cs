namespace UavPms.IdentityService.Application.Common.Utilities;

public static class EmailNormalizer
{
    public static string Normalize(string email)
        => email.Trim().ToLowerInvariant();
}
