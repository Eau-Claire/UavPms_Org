using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Threading;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public class RabbitMqConnection(IConfiguration configuration)
{
    private readonly ConnectionFactory _connectionFactory = new()
    {
        HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
        UserName = configuration["RabbitMQ:UserName"] ?? "guest",
        Password = configuration["RabbitMQ:Password"] ?? "guest",
        Port = int.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : 5672
    };

    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return _connectionFactory.CreateConnectionAsync(cancellationToken);
    }
}
