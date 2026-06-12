# syntax=docker/dockerfile:1

# Parameterized build - supports Umbraco 16 (net9.0), 17 (net10.0), 18 (net10.0)
# Targets: chiseled (for docker-compose), final (alternate)
#
# Examples:
#   docker build --target chiseled --build-arg TARGET_FRAMEWORK=net9.0 ...
#   docker build --target chiseled --build-arg TARGET_FRAMEWORK=net10.0 ...

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

# Build stage
FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

# All configurable via build args
ARG TARGET_FRAMEWORK=net10.0
ARG UMBRACO_CMS_VERSION="[17.4.0,18.0.0)"
ARG BUILD_CONFIGURATION=Release
ARG PACKAGE_SOURCE=build/Release
ARG PACKAGE_LANE=legacy

# Copy config files
COPY global.json ./
COPY Directory.Build.props ./
COPY nuget.config ./
COPY build/docker-site/ArticulateDockerSite.csproj build/docker-site/
COPY build/docker-site/Program.cs build/docker-site/
COPY build/docker-site/appsettings.json build/docker-site/
COPY build/docker-site/nuget.config build/docker-site/
COPY build/docker-site/Options/ build/docker-site/Options/
COPY build/docker-site/Services/ build/docker-site/Services/
COPY ${PACKAGE_SOURCE}/ build/Release/

# Sanity check + restore + publish
RUN set -eux; \
    ARTICULATE_PKG_FILE=$(ls -1t build/Release/Articulate.[0-9]*.nupkg 2>/dev/null | grep -v '\.snupkg$' | head -n 1 || true); \
    if [ -n "$ARTICULATE_PKG_FILE" ]; then \
      ARTICULATE_PKG_VERSION=$(basename "$ARTICULATE_PKG_FILE"); \
      ARTICULATE_PKG_VERSION=${ARTICULATE_PKG_VERSION#Articulate.}; \
      ARTICULATE_PKG_VERSION=${ARTICULATE_PKG_VERSION%.nupkg}; \
    else \
      ARTICULATE_PKG_VERSION=0.0.0; \
    fi; \
    echo "Building Articulate ${ARTICULATE_PKG_VERSION} for ${TARGET_FRAMEWORK} with Umbraco ${UMBRACO_CMS_VERSION}"; \
    dotnet restore build/docker-site/ArticulateDockerSite.csproj \
    --configfile build/docker-site/nuget.config \
    /p:TargetFramework=${TARGET_FRAMEWORK} \
    /p:UmbracoCmsPackageVersion=\"${UMBRACO_CMS_VERSION}\" \
    /p:ArticulatePackageVersion=\"$ARTICULATE_PKG_VERSION\"; \
    dotnet publish build/docker-site/ArticulateDockerSite.csproj \
    --no-restore \
    -c $BUILD_CONFIGURATION \
    -f ${TARGET_FRAMEWORK} \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:UmbracoCmsPackageVersion=\"${UMBRACO_CMS_VERSION}\" \
    /p:ArticulatePackageVersion=\"$ARTICULATE_PKG_VERSION\"

# ICU source for chiseled image (supports globalization on arm64)
FROM ${DOTNET_ASPNET_IMAGE} AS icu-source
RUN set -eux; \
    ARCH=$(dpkg --print-architecture); \
    case "$ARCH" in \
        amd64)  ICU_PATH=/usr/lib/x86_64-linux-gnu ;; \
        arm64)  ICU_PATH=/usr/lib/aarch64-linux-gnu ;; \
        *) echo "Unsupported arch: $ARCH"; exit 1 ;; \
    esac; \
    mkdir -p /staging; \
    cp "${ICU_PATH}"/libicudata* /staging/; \
    cp "${ICU_PATH}"/libicu* /staging/

# Chiseled runtime (used by docker-compose.yml)
FROM ${DOTNET_ASPNET_IMAGE}-noble-chiseled AS chiseled
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Container \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=icu-source /staging/ /usr/lib/
COPY --from=build /app/publish .

RUN set -eux; \
    mkdir -p /app/umbraco/Data /app/wwwroot/media /tmp; \
    chown -R 1654:1654 /app

USER 1654
VOLUME ["/app/umbraco/Data", "/app/wwwroot/media"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fs http://localhost:8080/umbraco || exit 1
ENTRYPOINT ["dotnet", "ArticulateDockerSite.dll"]

# Final stage (alternate, non-chiseled)
FROM ${DOTNET_ASPNET_IMAGE} AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Container \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN set -eux; \
    apt-get update; \
    apt-get dist-upgrade -y --no-install-recommends; \
    apt-get install -y --no-install-recommends ca-certificates curl sqlite3; \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN set -eux; \
    mkdir -p /app/umbraco/Data /app/wwwroot/media /tmp; \
    chown -R 1654:1654 /app

USER 1654
VOLUME ["/app/umbraco/Data", "/app/wwwroot/media"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fs http://localhost:8080/umbraco || exit 1
ENTRYPOINT ["dotnet", "ArticulateDockerSite.dll"]
