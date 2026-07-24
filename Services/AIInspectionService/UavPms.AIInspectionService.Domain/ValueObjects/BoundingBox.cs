namespace UavPms.AIInspectionService.Domain.ValueObjects;

// Trong C# hiện tại, từ khóa 'record' là cách hoàn hảo và ngắn gọn nhất 
// để tạo một Value Object . Nó tự động cung cấp tính bất biến (immutable)
// và so sánh theo giá trị chuẩn DDD (value-based equality)

public record BoundingBox(double X, double Y, double Width, double Height);