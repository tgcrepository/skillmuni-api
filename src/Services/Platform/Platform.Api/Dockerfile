# Build context: repository root.  docker build -f src/Services/Platform/Platform.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Platform/ src/Services/Platform/
RUN dotnet restore src/Services/Platform/Platform.Api/Platform.Api.csproj
RUN dotnet publish src/Services/Platform/Platform.Api/Platform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Platform.Api.dll"]
