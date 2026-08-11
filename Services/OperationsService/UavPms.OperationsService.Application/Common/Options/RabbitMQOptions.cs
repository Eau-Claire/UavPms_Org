using System.ComponentModel.DataAnnotations;

namespace UavPms.OperationsService.Application.Common.Options;

public class RabbitMQOptions
{
    public const string SectionName = "RabbitMQ";
    
    [Required(ErrorMessage = "RabbitMQ:HostName is required")]
    public string HostName { get; init; } = "localhost";
    
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public int Port { get; init; } = 5672;
}