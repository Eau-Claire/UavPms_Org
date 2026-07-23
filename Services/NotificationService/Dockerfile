FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Services/NotificationService/UavPms.NotificationService.csproj", "Services/NotificationService/"]
RUN dotnet restore "Services/NotificationService/UavPms.NotificationService.csproj"
COPY . .
WORKDIR "/src/Services/NotificationService"
RUN dotnet publish "UavPms.NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.NotificationService.dll"]
