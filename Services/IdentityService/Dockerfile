FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Services/IdentityService/UavPms.IdentityService.csproj", "Services/IdentityService/"]
RUN dotnet restore "Services/IdentityService/UavPms.IdentityService.csproj"
COPY . .
WORKDIR "/src/Services/IdentityService"
RUN dotnet publish "UavPms.IdentityService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.IdentityService.dll"]
