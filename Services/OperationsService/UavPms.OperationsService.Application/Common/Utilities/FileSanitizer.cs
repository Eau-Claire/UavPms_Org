using System.Text.RegularExpressions;

namespace UavPms.OperationsService.Application.Common.Utilities;

public class FileSanitizer
{
    public static readonly string[] DefaultAllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tiff", ".tif",
        ".mp4", ".avi", ".mov", ".webm"
    };
    
    /// <summary>
    /// Kiểm tra định dạng có năm trong danh sách cho phép hay kh
    /// </summary>
    public static bool IsAllowedExtension(string fileName, string[]? customAllowedExtensions = null)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        
        var allowed = customAllowedExtensions ?? DefaultAllowedExtensions;
        var safeFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        
        return !string.IsNullOrEmpty(extension) && allowed.Contains(extension);
    }
    
    /// <summary>
    /// Làm sạch tên file: loại bỏ đường dẫn tương đối và thay thế các ký tự không an toàn bằng dâu gạch dưới
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "unnamed_file";

        var safeName = Path.GetFileName(fileName);
        
        return Regex.Replace(safeName, @"[^\w\-.]", "_");
    }
    
    /// <summary>
    /// Tạo tên file duy nhất tránh đụng độ trên storage
    /// </summary>
    public static string GenerateUniqueFileName(string fileName)
    {
        var sanitized = SanitizeFileName(fileName);
        return $"{Guid.NewGuid()}_{sanitized}";
    }
}