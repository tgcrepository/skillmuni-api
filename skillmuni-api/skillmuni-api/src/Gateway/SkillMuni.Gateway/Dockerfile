# Build context: repository root.  docker build -f src/Gateway/SkillMuni.Gateway/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Gateway/ src/Gateway/
RUN dotnet restore src/Gateway/SkillMuni.Gateway/SkillMuni.Gateway.csproj
RUN dotnet publish src/Gateway/SkillMuni.Gateway/SkillMuni.Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "SkillMuni.Gateway.dll"]
