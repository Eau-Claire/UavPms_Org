namespace UavPms.AIInspectionService.Application.Common.Exceptions;

public class BusinessRuleException : Exception
{
    /// <summary>Machine-readable error code (same codes as OperationsService, e.g. TOWER_REQUIRED). NULL = generic.</summary>
    public string? Code { get; }

    public  BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string code, string message) : base(string.IsNullOrWhiteSpace(message) ? code : $"{code}: {message}")
    {
        Code = code;
    }
}
