namespace UavPms.OperationsService.Application.Common.Exceptions;

public class BusinessRuleException : Exception
{
    public string Code { get; }

    public BusinessRuleException(string message) : base(message)
    {
        Code = message;
    }

    public BusinessRuleException(string code, string message) : base(string.IsNullOrWhiteSpace(message) ? code : $"{code}: {message}")
    {
        Code = code;
    }

    public BusinessRuleException(string message, Exception innerException) : base(message, innerException)
    {
        Code = message;
    }

    public BusinessRuleException(string code, string message, Exception innerException) : base(string.IsNullOrWhiteSpace(message) ? code : $"{code}: {message}", innerException)
    {
        Code = code;
    }
}