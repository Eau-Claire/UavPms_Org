namespace UavPms.OperationsService.Domain.Enums;

public enum ReportType
{
    Defect = 0,    // Báo cáo khuyết tật AI
    Periodic = 1,  // Báo cáo kiểm tra định kỳ
    Thermal = 2,   // Báo cáo nhiệt ảnh
    Corridor = 3   // Báo cáo hành lang tuyến
}
