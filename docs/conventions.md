# UAV PMS - Codebase Conventions & Guidelines

Tài liệu này định nghĩa các quy chuẩn thiết kế, cấu trúc thư mục, quy tắc đặt tên, và quy ước phản hồi (response conventions) để đảm bảo tính đồng nhất trên toàn bộ codebase của dự án **UavPms**.

---

## 1. Cấu trúc Kiến trúc (Architecture Structure)

Dự án áp dụng mô hình **Clean Architecture** (Onion Architecture) chia thành các tầng rõ rệt:

*   **UavPms.Core**: Chứa các định nghĩa Domain như Entities, Enums, Interfaces (IRepository, IServices), ValueObjects, Models và các contract cốt lõi. Tầng này độc lập và không phụ thuộc vào bất kỳ tầng nào khác.
*   **UavPms.Application**: Triển khai các Use Case theo mô hình CQRS (sử dụng MediatR). Chứa các Features (Commands, Queries, DTOs) và Common (Behaviors, Exceptions, Common DTOs).
*   **UavPms.Infrastructure**: Triển khai các chi tiết kỹ thuật: Cơ sở dữ liệu (Persistence/EF Core/DbContext/Configurations), Repositories implementation, Services implementation (Email, OTP, Token, ...), Migrations, và Messaging.
*   **UavPms.WebApi**: Tầng giao tiếp client. Chứa Controllers, Filters, Middlewares, Background Jobs (Hangfire), và cấu hình startup.

---

## 2. Quy ước cấu trúc thư mục & File (Folder & File Conventions)

### 2.1. Cấu trúc CQRS trong `UavPms.Application`
Mỗi tính năng chính được đặt trong thư mục `Features/<FeatureName>` và chia thành:
*   `Commands/`: Chứa các hành động thay đổi trạng thái (Write).
*   `Queries/`: Chứa các hành động truy vấn dữ liệu (Read).
*   `DTOs/`: Chứa các Data Transfer Object phục vụ riêng cho Feature đó.

**Quy tắc đặt tên thư mục con của Command/Query:**
*   Mỗi Command hoặc Query phải có thư mục riêng trùng tên với tiền tố của Command/Query đó.
*   *Ví dụ tốt:* Thư mục `CreateMission/` chứa `CreateMissionCommand.cs` và `CreateMissionCommandHandler.cs`.
*   *Tránh:* Dùng tên rút gọn không đồng bộ như `Create/` (nên sửa thành `CreateNotification/`).

---

## 3. Quy tắc đặt tên (Naming Conventions)

### 3.1. Class & Interface
*   **Interface**: Bắt đầu bằng chữ `I` và viết theo PascalCase (ví dụ: `IUserRepository`, `IEmailService`).
*   **Controller**: Kết thúc bằng hậu tố `Controller` (ví dụ: `UserController`, `MissionController`).
*   **Repository Implementation**: Kết thúc bằng hậu tố `Repository` (ví dụ: `UserRepository`).
*   **Service Implementation**: Kết thúc bằng hậu tố `Service` (ví dụ: `EmailService`).

### 3.2. CQRS Components
*   **Command**: Đặt tên theo dạng `<Hành động><Thực thể>Command` (ví dụ: `CreateMissionCommand`, `SuspendUserCommand`).
*   **Query**: Đặt tên theo dạng `<Hành động><Thực thể>Query` (ví dụ: `GetUsersQuery`, `GetMissionDetailsQuery`).
*   **Handler**: Đặt tên tương ứng `<Command/QueryName>Handler` (ví dụ: `CreateMissionCommandHandler`, `GetUsersQueryHandler`).

### 3.3. DTO & Request DTO
*   **DTO trả về**: Kết thúc bằng hậu tố `Dto` (ví dụ: `MissionDto`, `UserDetailDto`).
*   **Paginated Response**: Đặt tên dạng `Paginated<Entities>Response` sử dụng kiểu dữ liệu `record` (ví dụ: `PaginatedMissionsResponse`).
*   **Request DTO (Client gửi lên)**:
    *   Đặt tên dạng `<Hành động><Thực thể>Request` hoặc `<Hành động><Thực thể>RequestDto` (ví dụ: `UpdateUserRequestDto`, `LoginRequest`).
    *   **Vị trí khai báo:** Phải được khai báo ở **cuối file Controller tương ứng**, nằm ngoài dấu đóng ngoặc nhọn `}` của class Controller.

---

## 4. Quy ước phản hồi API (ApiResponse Wrapper Conventions)

Để đảm bảo tính nhất quán của dữ liệu trả về cho Client, toàn bộ các API thành công (trừ các trường hợp trả về File/Stream đặc biệt) đều phải được bọc trong một cấu trúc chuẩn `ApiResponse`.

### Cấu trúc `ApiResponse`
```csharp
public record ApiResponse(bool Success, string Message, object? Data = null, object? Errors = null);
```

### Quy tắc áp dụng tại Controller:
*   **Trả về dữ liệu thành công (có data):**
    ```csharp
    return Ok(new ApiResponse(true, "Users retrieved successfully.", result));
    ```
*   **Trả về thông báo thành công (không có data):**
    ```csharp
    return Ok(new ApiResponse(true, "Password reset successfully."));
    ```
*   **Với các Endpoint trả về Object vô danh hoặc cấu trúc tùy biến:** Phải wrap các thuộc tính tùy biến vào trường `Data` của `ApiResponse`.
    ```csharp
    // Ví dụ enqueued job thành công
    return Ok(new ApiResponse(true, "Email job successfully enqueued in Hangfire.", new { JobId = jobId }));
    ```

---

## 5. Quy tắc phân trang (Pagination)

*   Sử dụng chung record `PaginationMetaData` tại namespace `UavPms.Application.Common.DTOs` để biểu diễn thông tin phân trang:
    ```csharp
    namespace UavPms.Application.Common.DTOs;

    public record PaginationMetaData(
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages
    );
    ```
*   Các response phân trang phải có định dạng chứa danh sách kết quả (`Items`) và thông tin phân trang (`Pagination`):
    ```csharp
    public record PaginatedMissionsResponse(
        List<MissionDto> Items,
        PaginationMetaData Pagination
    );
    ```
