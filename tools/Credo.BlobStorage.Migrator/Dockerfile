# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Credo.BlobStorage.sln ./
COPY tools/Credo.BlobStorage.Migrator/Credo.BlobStorage.Migrator.csproj tools/Credo.BlobStorage.Migrator/
COPY src/Credo.BlobStorage.Core/Credo.BlobStorage.Core.csproj src/Credo.BlobStorage.Core/

# Restore dependencies
RUN dotnet restore tools/Credo.BlobStorage.Migrator/Credo.BlobStorage.Migrator.csproj

# Copy source code
COPY tools/ tools/
COPY src/Credo.BlobStorage.Core/ src/Credo.BlobStorage.Core/

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