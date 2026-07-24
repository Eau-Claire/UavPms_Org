namespace UavPms.OperationsService.Domain.Interfaces.Services;

public interface IPasswordHasher
{
    // Băm mật khẩu người dùng thành một chuỗi mã hóa không thể dịch ngược  
    string Hash(string password);
    // So sánh mật khẩu người dùng với chuỗi đã mã hóa trong DB xem có trùng khớp không
    bool Verify(string passwordHash, string inputPassword);
}