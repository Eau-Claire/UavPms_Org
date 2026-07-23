FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Services/OperationsService/UavPms.OperationsService.csproj", "Services/OperationsService/"]
RUN dotnet restore "Services/OperationsService/UavPms.OperationsService.csproj"
COPY . .
WORKDIR "/src/Services/OperationsService"
RUN dotnet publish "UavPms.OperationsService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.OperationsService.dll"]
