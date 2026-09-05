namespace UavPms.OperationsService.Application.Common.Exceptions;

public sealed class InfrastructureOperationException : Exception
{
    public string ErrorCode { get; }
    public InfrastructureOperationException(string errorCode, string message, Exception inner)
        : base(message, inner) => ErrorCode = errorCode;
}
