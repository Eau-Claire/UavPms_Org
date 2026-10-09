using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Category",
                table: "SystemSettings",
                column: "Category");

            // Seed essential thresholds and system configurations
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var settings = new (string Key, string Value, string Description, string Category, string DataType)[]
            {
                // OTP
                ("Otp:ExpiryMinutes", "15", "Thời gian hết hạn của mã OTP tính theo phút", "Otp", "Int"),
                ("Otp:CooldownSeconds", "30", "Thời gian chờ trước khi cho phép gửi lại OTP tính theo giây", "Otp", "Int"),
                ("Otp:MaxAttempts", "5", "Số lần nhập sai OTP tối đa cho phép trước khi vô hiệu hóa mã", "Otp", "Int"),
                ("Otp:BypassCode", "123456", "Mã OTP kiểm thử mặc định cho môi trường Dev/Test", "Otp", "String"),

                // Authentication & Tokens
                ("Auth:AccessTokenExpiryMinutes", "60", "Thời gian hết hạn của JWT Access Token tính theo phút", "Auth", "Int"),
                ("Auth:RefreshTokenExpiryDays", "7", "Thời gian hết hạn của Refresh Token tính theo ngày", "Auth", "Int"),
                ("Auth:DeviceTrustDays", "30", "Thời gian hiệu lực của thiết bị tin cậy (Device Trust) tính theo ngày", "Auth", "Int"),
                ("Auth:StepUpTokenExpiryMinutes", "5", "Thời gian hiệu lực của Step-up Token tính theo phút", "Auth", "Int"),
                ("Auth:VerificationTokenExpiryMinutes", "10", "Thời gian hiệu lực của Verification Token quên mật khẩu tính theo phút", "Auth", "Int"),

                // AI & Evaluation
                ("Ai:EmergencyConfidenceThreshold", "0.80", "Ngưỡng độ tin cậy để kích hoạt cảnh báo khẩn cấp tức thời", "AI", "Double"),
                ("Ai:CriticalScoreThreshold", "90", "Ngưỡng điểm ưu tiên đánh giá mức độ Nghiêm trọng (Critical)", "AI", "Double"),
                ("Ai:HighScoreThreshold", "75", "Ngưỡng điểm ưu tiên đánh giá mức độ Cao (High)", "AI", "Double"),
                ("Ai:MediumScoreThreshold", "50", "Ngưỡng điểm ưu tiên đánh giá mức độ Trung bình (Medium)", "AI", "Double"),
                ("Ai:EmergencyWeight", "45", "Trọng số lớp khẩn cấp trong tính điểm ưu tiên", "AI", "Double"),
                ("Ai:MaxConfidenceWeight", "55", "Trọng số tối đa độ tin cậy trong tính điểm ưu tiên", "AI", "Double"),
                ("Ai:DefaultPreferredModel", "SERVER", "Mô hình AI mặc định được ưu tiên sử dụng", "AI", "String"),
                ("Ai:TimeoutSeconds", "60", "Thời gian chờ tối đa cho tác vụ phân tích AI tính theo giây", "AI", "Int"),

                // Drone & Technical Assessments
                ("Drone:CriticalBatteryThreshold", "30.0", "Ngưỡng pin tối thiểu (%) nếu dưới mức này UAV bị đánh giá hỏng hóc nghiêm trọng", "Drone", "Double"),
                ("Drone:WarningBatteryThreshold", "50.0", "Ngưỡng pin cảnh báo (%)", "Drone", "Double"),
                ("Drone:MinRfLinkThreshold", "50.0", "Ngưỡng tín hiệu sóng RF / Telemetry tối thiểu (%)", "Drone", "Double"),
                ("Drone:InspectionValidityDays", "7", "Thời hạn hiệu lực của phiếu kiểm định kỹ thuật drone tính theo ngày", "Drone", "Int")
            };

            foreach (var s in settings)
            {
                var id = Guid.NewGuid();
                migrationBuilder.Sql($@"
                    INSERT INTO ""SystemSettings"" (""Id"", ""Key"", ""Value"", ""Description"", ""Category"", ""DataType"", ""CreatedAt"", ""IsDeleted"")
                    VALUES ('{id}', '{s.Key}', '{s.Value}', '{s.Description.Replace("'", "''")}', '{s.Category}', '{s.DataType}', '{now:O}', false)
                    ON CONFLICT (""Key"") DO NOTHING;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
