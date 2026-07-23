FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Services/AIInspectionService/UavPms.AIInspectionService.csproj", "Services/AIInspectionService/"]
RUN dotnet restore "Services/AIInspectionService/UavPms.AIInspectionService.csproj"
COPY . .
WORKDIR "/src/Services/AIInspectionService"
RUN dotnet publish "UavPms.AIInspectionService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.AIInspectionService.dll"]
