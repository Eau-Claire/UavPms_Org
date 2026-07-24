using UavPms.InspectionEvaluationService.Infrastructure.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

var grpcPort = builder.Configuration.GetValue<int?>("Grpc:Port")
    ?? builder.Configuration.GetValue<int?>("PORT")
    ?? 8080;
var healthPort = builder.Configuration.GetValue<int?>("Health:Port") ?? 8081;

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(grpcPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    if (healthPort != grpcPort)
    {
        options.ListenAnyIP(healthPort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
        });
    }
});

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGrpcService<InspectionEvaluationGrpcService>();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Json(new
{
    service = "inspection-evaluation-grpc",
    status = "running",
    protocol = "gRPC",
    grpcService = "uavpms.inspectionevaluation.InspectionEvaluation"
}));

app.Run();
