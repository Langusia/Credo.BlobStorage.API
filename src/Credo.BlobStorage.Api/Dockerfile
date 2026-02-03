# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Credo.BlobStorage.sln ./
COPY src/Credo.BlobStorage.Api/Credo.BlobStorage.Api.csproj src/Credo.BlobStorage.Api/
COPY src/Credo.BlobStorage.Core/Credo.BlobStorage.Core.csproj src/Credo.BlobStorage.Core/

# Restore dependencies
RUN dotnet restore src/Credo.BlobStorage.Api/Credo.BlobStorage.Api.csproj

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/Credo.BlobStorage.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create storage directory
RUN mkdir -p /mnt/storage && chmod 777 /mnt/storage

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Credo.BlobStorage.Api.dll"]
