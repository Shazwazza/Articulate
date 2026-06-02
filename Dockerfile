# syntax=docker/dockerfile:1

# Build stage - .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Allow overriding Umbraco CMS version for the container build
ARG UMBRACO_CMS_VERSION="[17.4.0,18.0.0)"
ARG BUILD_CONFIGURATION=Release
ARG PACKAGE_DIR=/articulate-packages

# Copy config files
COPY global.json ./
COPY Directory.Build.props ./
COPY nuget.config ./
COPY build/docker-site/ArticulateDockerSite.csproj build/docker-site/
COPY build/docker-site/Program.cs build/docker-site/
COPY build/docker-site/appsettings.json build/docker-site/
COPY build/docker-site/appsettings.Container.json build/docker-site/
COPY build/docker-site/nuget.config build/docker-site/
COPY build/Release/ build/Release/

# Copy nupkg to packages folder and extract version, then restore and publish
RUN set -eux; \
    mkdir -p $PACKAGE_DIR; \
    cp build/Release/*.nupkg $PACKAGE_DIR/; \
    mkdir -p build/docker-site/packages; \
    cp $PACKAGE_DIR/*.nupkg build/docker-site/packages/; \
    ARTICULATE_PKG_FILE=$(ls -1t build/Release/Articulate.[0-9]*.nupkg | grep -v '\.snupkg$' | head -n 1 | xargs -n 1 basename); \
    ARTICULATE_PKG_VERSION=${ARTICULATE_PKG_FILE#Articulate.}; \
    ARTICULATE_PKG_VERSION=${ARTICULATE_PKG_VERSION%.nupkg}; \
    dotnet restore build/docker-site/ArticulateDockerSite.csproj \
    --configfile build/docker-site/nuget.config \
    /p:TargetFramework=net10.0 \
    /p:UmbracoCmsPackageVersion=\"${UMBRACO_CMS_VERSION}\" \
    /p:ArticulatePackageVersion=\"$ARTICULATE_PKG_VERSION\"; \
    dotnet publish build/docker-site/ArticulateDockerSite.csproj \
    -c $BUILD_CONFIGURATION \
    -f net10.0 \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:UmbracoCmsPackageVersion=\"${UMBRACO_CMS_VERSION}\" \
    /p:ArticulatePackageVersion=\"$ARTICULATE_PKG_VERSION\"

# Runtime stage - ASP.NET 10.0
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Container \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DEBIAN_FRONTEND=noninteractive

RUN set -eux; \
    apt-get update; \
    apt-get dist-upgrade -y --no-install-recommends; \
    apt-get install -y --no-install-recommends ca-certificates curl sqlite3; \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Ensure writable mount points exist (UID 1654 = app in base image)
RUN set -eux; \
    mkdir -p /app/umbraco/Data /app/wwwroot/media /tmp; \
    chown -R 1654:1654 /app

USER 1654

VOLUME ["/app/umbraco/Data", "/app/wwwroot/media"]
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fs http://localhost:8080/umbraco || exit 1

ENTRYPOINT ["dotnet", "ArticulateDockerSite.dll"]
