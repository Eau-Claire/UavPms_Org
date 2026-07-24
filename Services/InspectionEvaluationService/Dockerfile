FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.csproj", "Services/InspectionEvaluationService/"]
COPY ["Protos/inspection_evaluation.proto", "Protos/"]
RUN dotnet restore "Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.csproj"

COPY . .
WORKDIR "/src/Services/InspectionEvaluationService"
RUN dotnet publish "UavPms.InspectionEvaluationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "UavPms.InspectionEvaluationService.dll"]
