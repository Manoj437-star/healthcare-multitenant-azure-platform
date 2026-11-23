# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["MemberService.csproj", "./"]
RUN dotnet restore "MemberService.csproj"

COPY . .
RUN dotnet build "MemberService.csproj" -c Release -o /app/build
RUN dotnet publish "MemberService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as non-root user for security / HIPAA compliance
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MemberService.dll"]
