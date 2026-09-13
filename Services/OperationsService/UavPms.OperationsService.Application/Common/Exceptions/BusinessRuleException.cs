namespace UavPms.OperationsService.Application.Common.Exceptions;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string code, string message) : base(string.IsNullOrWhiteSpace(message) ? code : $"{code}: {message}")
    {
    }

    public BusinessRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}