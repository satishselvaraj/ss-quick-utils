# ============================================================
# GitLab OAuth Token Generator V3 - Docker Image
# ============================================================
# Multi-stage build for a minimal production image.
#
# Build:  docker build -t gitlab-oauth-token-gen .
# Run:    docker run -d -p 8585:8585 --name oauth-token-gen gitlab-oauth-token-gen
# Open:   http://localhost:8585
# Stop:   docker stop oauth-token-gen
# Remove: docker rm oauth-token-gen
# ============================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (cached layer)
COPY OAuthTokenGenV3.csproj .
RUN dotnet restore

# Copy everything else and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime (minimal image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose the port
ENV PORT=8585
EXPOSE 8585

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8585/ || exit 1

# Run the application
ENTRYPOINT ["dotnet", "OAuthTokenGenV3.dll"]
