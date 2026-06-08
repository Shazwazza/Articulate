# syntax=docker/dockerfile:1

# Unified Dockerfile. The local/CI smoke path is the chiseled runtime target.
# Usage examples:
#   docker build -t articulate:chiseled --target chiseled .

# Global versions (override with --build-arg)
ARG DOTNET_SDK_VERSION=10.0
ARG DOTNET_ASPNET_VERSION=10.0
ARG TARGET_FRAMEWORK=net10.0
ARG BUILD_CONFIGURATION=Release

# -------------------------
# Build stage (SDK) - performs restore & publish
# -------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS build
ARG TARGET_FRAMEWORK=net10.0
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project and required files for restore/publish
COPY global.json ./
COPY Directory.Build.props ./
COPY nuget.config ./
COPY build/docker-site/ArticulateDockerSite.csproj build/docker-site/
COPY build/docker-site/Program.cs build/docker-site/
COPY build/docker-site/appsettings.json build/docker-site/
COPY build/docker-site/nuget.config build/docker-site/
COPY build/docker-site/Options/ build/docker-site/Options/
COPY build/docker-site/Services/ build/docker-site/Services/
COPY build/Release/ build/Release/

# Copy nupkgs, extract Articulate package version, then restore & publish
RUN set -eux; \
    ARTICULATE_PKG_FILE=$(ls -1t build/Release/Articulate.[0-9]*.nupkg 2>/dev/null | grep -v '\.snupkg$' | head -n 1 || true); \
    if [ -n "$ARTICULATE_PKG_FILE" ]; then \
      ARTICULATE_PKG_VERSION=$(basename "$ARTICULATE_PKG_FILE"); \
      ARTICULATE_PKG_VERSION=${ARTICULATE_PKG_VERSION#Articulate.}; \
      ARTICULATE_PKG_VERSION=${ARTICULATE_PKG_VERSION%.nupkg}; \
    else \
      ARTICULATE_PKG_VERSION=0.0.0; \
    fi; \
    dotnet restore build/docker-site/ArticulateDockerSite.csproj \
      --configfile build/docker-site/nuget.config \
      /p:TargetFramework=${TARGET_FRAMEWORK} \
      /p:ArticulatePackageVersion=\"$ARTICULATE_PKG_VERSION\"; \
    dotnet publish build/docker-site/ArticulateDockerSite.csproj \
      --no-restore -c $BUILD_CONFIGURATION -f ${TARGET_FRAMEWORK} -o /app/publish \
      /p:UseAppHost=false /p:TargetFramework=${TARGET_FRAMEWORK} \
      /p:ArticulatePackageVersion=\"$ARTICULATE_PKG_VERSION\"

# -------------------------
# ICU source stage used to copy globalization libraries into the chiseled image.
# -------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_ASPNET_VERSION} AS icu-source
# Expose to copy native libs

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_ASPNET_VERSION}-noble-chiseled AS runtime-chiseled
WORKDIR /app

# -------------------------
# Chiseled runtime target
# -------------------------
FROM runtime-chiseled AS chiseled
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Container \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Copy ICU native libs from icu-source into chiseled image so chiseled supports globalization
COPY --from=icu-source /usr/lib/x86_64-linux-gnu/libicudata* /usr/lib/
COPY --from=icu-source /usr/lib/x86_64-linux-gnu/libicu* /usr/lib/

COPY --from=build --chown=1654:1654 /app/publish .
COPY --chown=1654:1654 build/docker-site/docker-emptydirs/umbraco/Data /app/umbraco/Data
COPY --chown=1654:1654 build/docker-site/docker-emptydirs/wwwroot/media /app/wwwroot/media
COPY --chown=1654:1654 build/docker-site/docker-emptydirs/Views /app/Views

USER 1654
VOLUME ["/app/umbraco/Data", "/app/wwwroot/media"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArticulateDockerSite.dll"]
