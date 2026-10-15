# Build context: repository root.  docker build -f src/Services/Cv/Cv.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Cv/ src/Services/Cv/
RUN dotnet restore src/Services/Cv/Cv.Api/Cv.Api.csproj
RUN dotnet publish src/Services/Cv/Cv.Api/Cv.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cv.Api.dll"]
