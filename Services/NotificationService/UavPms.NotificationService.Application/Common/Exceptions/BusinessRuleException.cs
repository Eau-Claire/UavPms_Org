namespace UavPms.NotificationService.Application.Common.Exceptions;

public class BusinessRuleException : Exception
{
    public string? Code { get; }

    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string code, string message)
        : base(string.IsNullOrWhiteSpace(message) ? code : $"{code}: {message}")
    {
        Code = code;
    }
}