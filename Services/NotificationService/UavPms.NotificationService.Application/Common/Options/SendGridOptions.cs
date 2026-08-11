using System.ComponentModel.DataAnnotations;

namespace UavPms.NotificationService.Application.Common.Options;

public class SendGridOptions
{
    public const string SectionName = "SendGrid";
    
    [Required(ErrorMessage = "ApiKey is required")]
    public string ApiKey { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "FromEmail is required")]
    [EmailAddress(ErrorMessage = "FromEmail must be a valid email address")]
    public string FromEmail { get; init; } = string.Empty;

    public string FromName { get; init; } = "UavPms Notification System";

}