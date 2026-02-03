# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Credo.BlobStorage.sln ./
COPY tools/Credo.BlobStorage.Migrator/Credo.BlobStorage.Migrator.csproj tools/Credo.BlobStorage.Migrator/

# Restore dependencies
RUN dotnet restore tools/Credo.BlobStorage.Migrator/Credo.BlobStorage.Migrator.csproj

# Copy source code
COPY tools/ tools/

# Build and publish
WORKDIR /src/tools/Credo.BlobStorage.Migrator
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy published app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Credo.BlobStorage.Migrator.dll"]