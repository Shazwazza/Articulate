# Dev Container for Articulate
# Provides: .NET 10 SDK, Node 24, pnpm, nbgv, sqlite, GitHub CLI, Copilot CLI.

FROM mcr.microsoft.com/devcontainers/dotnet:dev-10.0

# Install Node.js 24 and the pinned pnpm release.
ARG NODE_VERSION=24
ARG PNPM_VERSION=11.19.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends bash ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL https://deb.nodesource.com/setup_${NODE_VERSION}.x | bash - \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs sqlite3 libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

ENV SHELL=/bin/bash
ENV PNPM_HOME="/usr/local/share/pnpm"
ENV PATH="${PNPM_HOME}/bin:${PATH}"
RUN curl -fsSL https://get.pnpm.io/install.sh -o /tmp/install-pnpm.sh \
    && SHELL=/bin/bash PNPM_VERSION=${PNPM_VERSION} sh /tmp/install-pnpm.sh \
    && rm /tmp/install-pnpm.sh \
    && node --version \
    && pnpm --version

# Install GitHub Copilot CLI.
RUN npm install -g @github/copilot \
    && copilot --version

# Optional CLI examples; uncomment any you want:
# RUN npm install -g --ignore-scripts @earendil-works/pi-coding-agent \
#     && pi --version
# RUN curl -fsSL https://chatgpt.com/codex/install.sh | sh \
#     && codex --version
# RUN dotnet tool install --global ilspycmd

# Install nbgv as global tool
RUN dotnet tool install -g nbgv

# Add dotnet tools to PATH
ENV PATH="${PATH}:/root/.dotnet/tools"
