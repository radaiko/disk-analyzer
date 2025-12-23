# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["src/DiskAnalyzer/DiskAnalyzer.csproj", "src/DiskAnalyzer/"]
RUN dotnet restore "src/DiskAnalyzer/DiskAnalyzer.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/DiskAnalyzer"
RUN dotnet build "DiskAnalyzer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DiskAnalyzer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install dependencies for file system scanning
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create config directory
RUN mkdir -p /app/config

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the app
ENTRYPOINT ["dotnet", "DiskAnalyzer.dll"]
