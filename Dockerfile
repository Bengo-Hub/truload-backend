#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 4000
EXPOSE 4001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["truload-backend.csproj", "."]
RUN dotnet restore "./truload-backend.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./truload-backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./truload-backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
# Use SDK image instead of aspnet to have EF Core tools available for migrations

# Install curl and postgresql-client for health checks and DB operations
RUN apt-get update && apt-get install -y curl postgresql-client && rm -rf /var/lib/apt/lists/*

# Install EF Core tools globally for migrations
RUN dotnet tool install --global dotnet-ef --version 8.0.*
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user for runtime (security)
RUN useradd -m -u 1001 appuser && chown -R appuser:appuser /app
USER appuser

EXPOSE 4000
EXPOSE 4001

# Configure ASP.NET Core to listen on port 4000 (standardized across all backend apps)
ENV ASPNETCORE_URLS=http://+:4000
ENV ASPNETCORE_HTTP_PORTS=4000

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -fsS http://localhost:4000/health || exit 1

ENTRYPOINT ["dotnet", "truload-backend.dll"]