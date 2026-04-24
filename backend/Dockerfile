# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first for layer caching
COPY nuget.config ./
COPY src/PortfolioThermometer.Core/PortfolioThermometer.Core.csproj                         src/PortfolioThermometer.Core/
COPY src/PortfolioThermometer.Infrastructure/PortfolioThermometer.Infrastructure.csproj     src/PortfolioThermometer.Infrastructure/
COPY src/PortfolioThermometer.Api/PortfolioThermometer.Api.csproj                           src/PortfolioThermometer.Api/

RUN dotnet restore src/PortfolioThermometer.Api/PortfolioThermometer.Api.csproj

# Copy remaining source files
COPY . .

# Publish API in Release configuration
RUN dotnet publish src/PortfolioThermometer.Api/PortfolioThermometer.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "PortfolioThermometer.Api.dll"]
