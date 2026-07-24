FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.API/UavPms.InspectionEvaluationService.API.csproj", "Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.API/"]
COPY ["Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Application/UavPms.InspectionEvaluationService.Application.csproj", "Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Application/"]
COPY ["Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Domain/UavPms.InspectionEvaluationService.Domain.csproj", "Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Domain/"]
COPY ["Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Infrastructure/UavPms.InspectionEvaluationService.Infrastructure.csproj", "Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Infrastructure/"]
COPY ["Protos/inspection_evaluation.proto", "Protos/"]

RUN dotnet restore "Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.API/UavPms.InspectionEvaluationService.API.csproj"

COPY . .
WORKDIR "/src/Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.API"
RUN dotnet publish "UavPms.InspectionEvaluationService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UavPms.InspectionEvaluationService.API.dll"]
