FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Services/NotificationService/UavPms.NotificationService.API/UavPms.NotificationService.API.csproj", "Services/NotificationService/UavPms.NotificationService.API/"]
COPY ["Services/NotificationService/UavPms.NotificationService.Application/UavPms.NotificationService.Application.csproj", "Services/NotificationService/UavPms.NotificationService.Application/"]
COPY ["Services/NotificationService/UavPms.NotificationService.Domain/UavPms.NotificationService.Domain.csproj", "Services/NotificationService/UavPms.NotificationService.Domain/"]
COPY ["Services/NotificationService/UavPms.NotificationService.Infrastructure/UavPms.NotificationService.Infrastructure.csproj", "Services/NotificationService/UavPms.NotificationService.Infrastructure/"]
COPY ["Shared/UavPms.Shared.Contracts/UavPms.Shared.Contracts.csproj", "Shared/UavPms.Shared.Contracts/"]

RUN dotnet restore "Services/NotificationService/UavPms.NotificationService.API/UavPms.NotificationService.API.csproj"

COPY . .
WORKDIR "/src/Services/NotificationService/UavPms.NotificationService.API"
RUN dotnet publish "UavPms.NotificationService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.NotificationService.API.dll"]
