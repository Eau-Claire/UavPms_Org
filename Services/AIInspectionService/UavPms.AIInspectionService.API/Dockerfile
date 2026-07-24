FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Services/AIInspectionService/UavPms.AIInspectionService.API/UavPms.AIInspectionService.API.csproj", "Services/AIInspectionService/UavPms.AIInspectionService.API/"]
COPY ["Services/AIInspectionService/UavPms.AIInspectionService.Application/UavPms.AIInspectionService.Application.csproj", "Services/AIInspectionService/UavPms.AIInspectionService.Application/"]
COPY ["Services/AIInspectionService/UavPms.AIInspectionService.Domain/UavPms.AIInspectionService.Domain.csproj", "Services/AIInspectionService/UavPms.AIInspectionService.Domain/"]
COPY ["Services/AIInspectionService/UavPms.AIInspectionService.Infrastructure/UavPms.AIInspectionService.Infrastructure.csproj", "Services/AIInspectionService/UavPms.AIInspectionService.Infrastructure/"]
COPY ["Shared/UavPms.Shared.Contracts/UavPms.Shared.Contracts.csproj", "Shared/UavPms.Shared.Contracts/"]
COPY ["Protos/inspection_evaluation.proto", "Protos/"]

RUN dotnet restore "Services/AIInspectionService/UavPms.AIInspectionService.API/UavPms.AIInspectionService.API.csproj"

COPY . .
WORKDIR "/src/Services/AIInspectionService/UavPms.AIInspectionService.API"
RUN dotnet publish "UavPms.AIInspectionService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.AIInspectionService.API.dll"]
