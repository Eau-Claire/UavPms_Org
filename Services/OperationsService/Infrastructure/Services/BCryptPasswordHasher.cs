using BCrypt.Net;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Infrastructure.Services;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 10;

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string passwordHash, string inputPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(inputPassword))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(inputPassword, passwordHash);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
