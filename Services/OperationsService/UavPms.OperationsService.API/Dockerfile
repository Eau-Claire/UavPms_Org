FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Services/OperationsService/UavPms.OperationsService.API/UavPms.OperationsService.API.csproj", "Services/OperationsService/UavPms.OperationsService.API/"]
COPY ["Services/OperationsService/UavPms.OperationsService.Application/UavPms.OperationsService.Application.csproj", "Services/OperationsService/UavPms.OperationsService.Application/"]
COPY ["Services/OperationsService/UavPms.OperationsService.Domain/UavPms.OperationsService.Domain.csproj", "Services/OperationsService/UavPms.OperationsService.Domain/"]
COPY ["Services/OperationsService/UavPms.OperationsService.Infrastructure/UavPms.OperationsService.Infrastructure.csproj", "Services/OperationsService/UavPms.OperationsService.Infrastructure/"]
COPY ["Shared/UavPms.Shared.Contracts/UavPms.Shared.Contracts.csproj", "Shared/UavPms.Shared.Contracts/"]

RUN dotnet restore "Services/OperationsService/UavPms.OperationsService.API/UavPms.OperationsService.API.csproj"

COPY . .
WORKDIR "/src/Services/OperationsService/UavPms.OperationsService.API"
RUN dotnet publish "UavPms.OperationsService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.OperationsService.API.dll"]
