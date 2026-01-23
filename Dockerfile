# DataChat Dockerfile
# Multi-stage build for optimal image size

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ["DataChat.sln", "."]
COPY ["src/Core/DataChat.Domain/DataChat.Domain.csproj", "src/Core/DataChat.Domain/"]
COPY ["src/Core/DataChat.Application/DataChat.Application.csproj", "src/Core/DataChat.Application/"]
COPY ["src/Infrastructure/DataChat.Infrastructure/DataChat.Infrastructure.csproj", "src/Infrastructure/DataChat.Infrastructure/"]
COPY ["src/Presentation/DataChat.Web/DataChat.Web.csproj", "src/Presentation/DataChat.Web/"]

# Restore dependencies
RUN dotnet restore "src/Presentation/DataChat.Web/DataChat.Web.csproj"

# Copy everything else
COPY . .

# Build and publish
WORKDIR "/src/src/Presentation/DataChat.Web"
RUN dotnet publish "DataChat.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Tesseract OCR and dependencies for document processing
RUN apt-get update && apt-get install -y --no-install-recommends \
    tesseract-ocr \
    tesseract-ocr-eng \
    libtesseract-dev \
    libleptonica-dev \
    libgdiplus \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create directories for persistent data
RUN mkdir -p /app/data-protection-keys /app/logs /app/uploads /app/certs

# Copy published application
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl --fail http://localhost:8080/health/live || exit 1

# Run the application
ENTRYPOINT ["dotnet", "DataChat.Web.dll"]
