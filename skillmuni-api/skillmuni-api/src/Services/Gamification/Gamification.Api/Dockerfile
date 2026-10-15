# Build context: repository root.  docker build -f src/Services/Gamification/Gamification.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Gamification/ src/Services/Gamification/
RUN dotnet restore src/Services/Gamification/Gamification.Api/Gamification.Api.csproj
RUN dotnet publish src/Services/Gamification/Gamification.Api/Gamification.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gamification.Api.dll"]
