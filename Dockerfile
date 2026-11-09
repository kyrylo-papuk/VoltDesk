# API image. Build from the repository root: docker build -t voltdesk-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the package layer is reused while only source code changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/VoltDesk.Core/VoltDesk.Core.csproj src/VoltDesk.Core/
COPY src/VoltDesk.Contracts/VoltDesk.Contracts.csproj src/VoltDesk.Contracts/
COPY src/VoltDesk.Infrastructure/VoltDesk.Infrastructure.csproj src/VoltDesk.Infrastructure/
COPY src/VoltDesk.Api/VoltDesk.Api.csproj src/VoltDesk.Api/
RUN dotnet restore src/VoltDesk.Api/VoltDesk.Api.csproj

COPY src/VoltDesk.Core/ src/VoltDesk.Core/
COPY src/VoltDesk.Contracts/ src/VoltDesk.Contracts/
COPY src/VoltDesk.Infrastructure/ src/VoltDesk.Infrastructure/
COPY src/VoltDesk.Api/ src/VoltDesk.Api/
RUN dotnet publish src/VoltDesk.Api/VoltDesk.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# .NET images ship a non-root "app" user; the API listens on 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "VoltDesk.Api.dll"]
