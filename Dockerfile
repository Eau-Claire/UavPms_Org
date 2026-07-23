# Default production image: Ocelot API Gateway
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["UavPms.ApiGateway/UavPms.ApiGateway.csproj", "UavPms.ApiGateway/"]
RUN dotnet restore "UavPms.ApiGateway/UavPms.ApiGateway.csproj"

COPY . .
WORKDIR "/src/UavPms.ApiGateway"
RUN dotnet publish "UavPms.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "UavPms.ApiGateway.dll"]
