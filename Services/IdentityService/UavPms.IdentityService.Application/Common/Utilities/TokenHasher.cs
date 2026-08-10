using System.Security.Cryptography;
using System.Text;

namespace UavPms.IdentityService.Application.Common.Utilities;

public static class TokenHasher
{
    ///// <summary>
    ///// Nhận chuỗi token vào, trả về SHA256 Base64 hash.
    ///// </summary>

    public static string Hash(string token)
    {
        if(string.IsNullOrEmpty(token)) return string.Empty;
        
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}