FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Services/IdentityService/UavPms.IdentityService.API/UavPms.IdentityService.API.csproj", "Services/IdentityService/UavPms.IdentityService.API/"]
COPY ["Services/IdentityService/UavPms.IdentityService.Application/UavPms.IdentityService.Application.csproj", "Services/IdentityService/UavPms.IdentityService.Application/"]
COPY ["Services/IdentityService/UavPms.IdentityService.Domain/UavPms.IdentityService.Domain.csproj", "Services/IdentityService/UavPms.IdentityService.Domain/"]
COPY ["Services/IdentityService/UavPms.IdentityService.Infrastructure/UavPms.IdentityService.Infrastructure.csproj", "Services/IdentityService/UavPms.IdentityService.Infrastructure/"]
COPY ["Shared/UavPms.Shared.Contracts/UavPms.Shared.Contracts.csproj", "Shared/UavPms.Shared.Contracts/"]

RUN dotnet restore "Services/IdentityService/UavPms.IdentityService.API/UavPms.IdentityService.API.csproj"

COPY . .
WORKDIR "/src/Services/IdentityService/UavPms.IdentityService.API"
RUN dotnet publish "UavPms.IdentityService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.IdentityService.API.dll"]
