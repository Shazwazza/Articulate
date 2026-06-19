# Dev Container for Articulate
# Provides: .NET 9 + 10 SDKs, Node 24, pnpm, nbgv, sqlite

FROM mcr.microsoft.com/devcontainers/dotnet:dev-10.0

# Install .NET 9 SDK (side-by-side with 10)
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir /usr/share/dotnet

# Install Node.js 24
ARG NODE_VERSION=24
RUN curl -fsSL https://deb.nodesource.com/setup_${NODE_VERSION}.x | bash - \
    && apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates nodejs sqlite3 libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

# Enable Corepack and set pnpm version
RUN corepack enable \
    && corepack prepare pnpm@11.4.0 --activate


# Install nbgv as global tool
RUN dotnet tool install -g nbgv

# Add dotnet tools to PATH
ENV PATH="${PATH}:/root/.dotnet/tools"
