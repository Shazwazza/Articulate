# syntax=docker/dockerfile:1

# Parameterized build - supports Umbraco 17 and 18 on net10.0
# Targets: chiseled (for docker-compose), final (alternate)
#
# Examples:
#   docker build --target chiseled --build-arg TARGET_FRAMEWORK=net10.0 ...

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

# Build stage
FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

# All configurable via build args
ARG TARGET_FRAMEWORK=net10.0
# UMBRACO_CMS_VERSION is required; run.cs reads the floor from Directory.Packages.props
# and passes it as --build-arg. No default — explicit fail beats silent version drift.
ARG UMBRACO_CMS_VERSION
ARG BUILD_CONFIGURATION=Release
ARG PACKAGE_SOURCE=build/Release/v17
ARG USE_TINYMCE_UMBRACO=false
# TINYMCE_UMBRACO_PACKAGE_VERSION is required when USE_TINYMCE_UMBRACO=true;
# run.cs reads it from Directory.Packages.props and passes as --build-arg.
ARG TINYMCE_UMBRACO_PACKAGE_VERSION

# Copy config files
COPY global.json ./
COPY Directory.Build.props ./
COPY nuget.config ./
COPY docker/src/ArticulateDockerSite.csproj docker/src/
COPY docker/src/Program.cs docker/src/
COPY docker/src/appsettings.json docker/src/
COPY docker/src/nuget.config docker/src/
COPY docker/src/Options/ docker/src/Options/
COPY docker/src/Services/ docker/src/Services/
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
    if [ -z "${UMBRACO_CMS_VERSION}" ]; then \
      echo 'ERROR: UMBRACO_CMS_VERSION build-arg is required. Pass --build-arg UMBRACO_CMS_VERSION=<range>, e.g. [17.6.2,18.1.0) for v17 or [18.1.1,19.0.0) for v18. build.cs reads the floor from Directory.Packages.props.'; \
      exit 1; \
    fi; \
    if [ "${USE_TINYMCE_UMBRACO}" = "true" ]; then \
      if [ -z "${TINYMCE_UMBRACO_PACKAGE_VERSION}" ]; then \
        echo 'ERROR: TINYMCE_UMBRACO_PACKAGE_VERSION build-arg is required when USE_TINYMCE_UMBRACO=true. build.cs reads the floor from Directory.Packages.props (per lane: [17.5.0,18.0.0) for v17, [18.0.0,19.0.0) for v18).'; \
        exit 1; \
      fi; \
    fi; \
    # MSBuild's CLI parser splits on `)` for /p:Name=Value, so CPM ranges like
    # `[17.5.3,18.0.0)` can't be passed that way. Setting them as environment
    # variables before the dotnet call lets MSBuild pick them up as properties
    # without going through the CLI parser. Names must match the property names
    # exactly (case-sensitive on Linux). UseTinyMceUmbraco stays as /p: — it's a
    # plain bool flag with no `)` in any value.
    export UmbracoCmsPackageVersion="${UMBRACO_CMS_VERSION}"; \
    if [ "${USE_TINYMCE_UMBRACO}" = "true" ]; then \
      export TinyMceUmbracoPackageVersion="${TINYMCE_UMBRACO_PACKAGE_VERSION}"; \
    fi; \
    echo "Building Articulate ${ARTICULATE_PKG_VERSION} for ${TARGET_FRAMEWORK} with Umbraco ${UMBRACO_CMS_VERSION}"; \
    dotnet restore docker/src/ArticulateDockerSite.csproj \
    --configfile docker/src/nuget.config \
    /p:TargetFramework=${TARGET_FRAMEWORK} \
    /p:UseTinyMceUmbraco=${USE_TINYMCE_UMBRACO} \
    /p:ArticulatePackageVersion=${ARTICULATE_PKG_VERSION}; \
    dotnet publish docker/src/ArticulateDockerSite.csproj \
    --no-restore \
    -c $BUILD_CONFIGURATION \
    -f ${TARGET_FRAMEWORK} \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:UseTinyMceUmbraco=${USE_TINYMCE_UMBRACO} \
    /p:ArticulatePackageVersion=${ARTICULATE_PKG_VERSION}; \
    mkdir -p /app/publish/umbraco/Data /app/publish/wwwroot/media

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
    cp "${ICU_PATH}"/libicu* /staging/

# Chiseled runtime (used by docker-compose.yml)
FROM ${DOTNET_ASPNET_IMAGE}-noble-chiseled AS chiseled
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Container \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=icu-source /staging/ /usr/lib/
COPY --from=build --chown=1654:1654 /app/publish .

USER 1654
VOLUME ["/app/umbraco/Data", "/app/wwwroot/media"]
EXPOSE 8080
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
