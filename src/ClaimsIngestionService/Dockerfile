# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ClaimsIngestionService.csproj", "./"]
RUN dotnet restore "ClaimsIngestionService.csproj"

COPY . .
RUN dotnet build "ClaimsIngestionService.csproj" -c Release -o /app/build
RUN dotnet publish "ClaimsIngestionService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ClaimsIngestionService.dll"]
