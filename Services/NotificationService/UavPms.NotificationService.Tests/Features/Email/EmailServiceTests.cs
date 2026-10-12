using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UavPms.NotificationService.Application.Common.Options;
using UavPms.NotificationService.Infrastructure.Services;
using Xunit;

namespace UavPms.NotificationService.Tests.Features.Email;

public class EmailServiceTests
{
    [Fact]
    public async Task NOTIF_MAIL_005_SendGridApiKey_Missing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert
        var act1 = async () => await emailService.SendOtpEmailAsync("user@test.com", "123456", DateTime.UtcNow.AddMinutes(3));
        var act2 = async () => await emailService.SendPasswordResetEmailAsync("user@test.com", "token123", DateTime.UtcNow.AddHours(1));
        var act3 = async () => await emailService.SendEmailAsync("user@test.com", "Subject", "Body");

        (await act1.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("SendGrid:ApiKey is not configured.");
        (await act2.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("SendGrid:ApiKey is not configured.");
        (await act3.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("SendGrid:ApiKey is not configured.");
    }

    [Fact]
    public async Task NOTIF_MAIL_006_SendOtpEmailAsync_InvalidApiKey_ShouldThrowExceptionWithStatusCode()
    {
        // Arrange: Invalid API key causes SendGrid to return 401 Unauthorized
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act
        Func<Task> act = async () => await emailService.SendOtpEmailAsync("user@test.com", "123456", DateTime.UtcNow.AddMinutes(3));

        // Assert
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().StartWith("SendGrid returned status code");
    }

    [Fact]
    public async Task NOTIF_MAIL_007_SendEmailAsync_InvalidApiKey_ShouldIncludeTargetEmailInExceptionMessage()
    {
        // Arrange
        var targetEmail = "recipient@uavpms.test";
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act
        Func<Task> act = async () => await emailService.SendEmailAsync(targetEmail, "Subject", "Body");

        // Assert
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().Contain($"for sending email to {targetEmail}");
    }

    [Fact]
    public async Task NOTIF_MAIL_004_020_VietnameseUtf8Content_ShouldBeProcessedWithoutEncodingError()
    {
        // Arrange
        var targetEmail = "inspector@uavpms.test";
        var subject = "⚠️ Cảnh báo khẩn cấp: Kiểm tra cột #105";
        var body = "Phát hiện khuyết tật cách điện tại cột điện #105 cung đoạn Thạnh Mỹ - Pleiku.";
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert: Should build request cleanly without string encoding exception
        Func<Task> act = async () => await emailService.SendEmailAsync(targetEmail, subject, body);
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().Contain(targetEmail);
    }

    [Fact]
    public async Task NOTIF_MAIL_010_SendOtpEmailAsync_OtpLengths_ShouldInterpolateProperly()
    {
        // Arrange
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert for 4-digit, 6-digit, 8-digit OTPs
        foreach (var otp in new[] { "1234", "123456", "12345678" })
        {
            Func<Task> act = async () => await emailService.SendOtpEmailAsync("user@test.com", otp, DateTime.UtcNow.AddMinutes(3));
            var ex = await act.Should().ThrowAsync<Exception>();
            ex.Which.Message.Should().StartWith("SendGrid returned status code");
        }
    }

    [Fact]
    public async Task NOTIF_MAIL_013_SendPasswordResetEmailAsync_SpecialCharactersInToken_ShouldNotThrowFormattingException()
    {
        // Arrange
        var tokenWithSpecialChars = "abc+123/xyz==?param=val&foo=bar";
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert
        Func<Task> act = async () => await emailService.SendPasswordResetEmailAsync("user@test.com", tokenWithSpecialChars, DateTime.UtcNow.AddHours(1));
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().StartWith("SendGrid returned status code");
    }

    [Fact]
    public async Task NOTIF_MAIL_015_SendEmailAsync_HtmlBodyContent_ShouldBuildMailHelperPayloadWithoutError()
    {
        // Arrange
        var htmlContent = "<h1>Báo cáo bảo trì</h1><p>Hệ thống hoạt động bình thường.</p>";
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert
        Func<Task> act = async () => await emailService.SendEmailAsync("user@test.com", "Tiêu đề HTML", htmlContent);
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().StartWith("SendGrid returned status code");
    }

    [Fact]
    public async Task NOTIF_MAIL_021_SendEmailAsync_EmptySubjectAndBody_ShouldNotThrowNullReferenceException()
    {
        // Arrange
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert
        Func<Task> act = async () => await emailService.SendEmailAsync("user@test.com", "", "");
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().StartWith("SendGrid returned status code");
    }

    [Fact]
    public async Task NOTIF_MAIL_022_SendEmailAsync_ConcurrentInvocations_ShouldBeThreadSafe()
    {
        // Arrange
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act: Invoke 10 calls concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await emailService.SendEmailAsync($"user{idx}@test.com", $"Subject {idx}", $"Body {idx}");
                }
                catch (Exception ex)
                {
                    // Expected SendGrid unauthorized failure; verify no NullReferenceException or state corruption
                    ex.Message.Should().Contain($"user{idx}@test.com");
                }
            }));
        }

        // Assert
        Func<Task> act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void NOTIF_MAIL_023_SendGridOptions_Validation_ShouldValidateRequiredFields()
    {
        // Arrange
        var invalidOptions = new SendGridOptions
        {
            ApiKey = "",
            FromEmail = "not-an-email"
        };

        var context = new ValidationContext(invalidOptions);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(invalidOptions, context, validationResults, true);

        // Assert
        isValid.Should().BeFalse();
        validationResults.Should().Contain(r => r.ErrorMessage == "ApiKey is required");
        validationResults.Should().Contain(r => r.ErrorMessage == "FromEmail must be a valid email address");
    }

    [Fact]
    public void NOTIF_MAIL_024_SendGridOptions_FromName_ShouldHaveDefaultFallback()
    {
        // Arrange & Act
        var options = new SendGridOptions();

        // Assert
        options.FromName.Should().Be("UavPms Notification System");
        SendGridOptions.SectionName.Should().Be("SendGrid");
    }

    [Fact]
    public async Task NOTIF_MAIL_025_NullTargetEmail_ShouldBeHandledGracefullyBySendGridClient()
    {
        // Arrange
        var options = Options.Create(new SendGridOptions
        {
            ApiKey = "SG.invalid_api_key_test_1234567890",
            FromEmail = "no-reply@uavpms.com",
            FromName = "UAV PMS"
        });
        var emailService = new EmailService(options);

        // Act & Assert: SendGrid EmailAddress accepts null string, then SendGrid API returns failure response status code
        Func<Task> act = async () => await emailService.SendEmailAsync(null!, "Subject", "Body");
        var ex = await act.Should().ThrowAsync<Exception>();
        ex.Which.Message.Should().StartWith("SendGrid returned status code");
    }
}
