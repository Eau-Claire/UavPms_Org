using System.IO;
using System.Threading.Tasks;

namespace UavPms.AIInspectionService.Domain.Interfaces.Services;

public interface IFileStorageService
{
    // Nhận vào một luồng dữ liệu (Stream) và tên file, trả về đường dẫn đã lưu
    Task<string> SaveImageAsync(Stream fileStream, string fileName);
    
    // Xóa ảnh khi cần thiết
    Task DeleteImageAsync(string imagePath);
}