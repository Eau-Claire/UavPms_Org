namespace UavPms.IdentityService.Infrastructure.Utilities;

internal static class EmailNormalizer
{
    public static string Normalize(string email)
        => email.Trim().ToLowerInvariant();
}
