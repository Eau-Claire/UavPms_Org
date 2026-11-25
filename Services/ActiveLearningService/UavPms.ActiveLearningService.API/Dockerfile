FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Services/ActiveLearningService/UavPms.ActiveLearningService.API/UavPms.ActiveLearningService.API.csproj", "Services/ActiveLearningService/UavPms.ActiveLearningService.API/"]
COPY ["Shared/UavPms.Shared.Contracts/UavPms.Shared.Contracts.csproj", "Shared/UavPms.Shared.Contracts/"]

RUN dotnet restore "Services/ActiveLearningService/UavPms.ActiveLearningService.API/UavPms.ActiveLearningService.API.csproj"

COPY Services/ActiveLearningService/UavPms.ActiveLearningService.API/ Services/ActiveLearningService/UavPms.ActiveLearningService.API/
COPY Shared/UavPms.Shared.Contracts/ Shared/UavPms.Shared.Contracts/
WORKDIR "/src/Services/ActiveLearningService/UavPms.ActiveLearningService.API"
RUN dotnet publish "UavPms.ActiveLearningService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.ActiveLearningService.API.dll"]
