namespace UavPms.NotificationService.Application.Common.Utilities;

public class NotificationFormatter
{
    public static string FormatTemplate(string template, IDictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(template) || parameters == null)
        {
            return template ?? string.Empty;
        }

        var result = template;
        foreach (var kvp in parameters)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value ?? string.Empty);
        }
        
        return result;
    }

    public static string BuildEmergencyAlertTitle(string anomalyType, string priority)
    {
        return $"[CẢNH BÁO KHẨN CẤP: {priority?.ToUpperInvariant()}] Phát hiện sự cố {priority}";
    }
}