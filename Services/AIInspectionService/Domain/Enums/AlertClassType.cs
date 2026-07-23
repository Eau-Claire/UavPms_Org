namespace UavPms.AIInspectionService.Domain.Enums;

public enum AlertClassType
{
    Human = 0,          // Phát hiện người
    NonHuman = 1,       // Không phải người 
    Unknown = 2         // Vật thể không xác định
    
    
    // Bổ sung và thay đổi khi bước qua giai đoạn 2
}