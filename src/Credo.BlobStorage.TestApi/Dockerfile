# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Credo.BlobStorage.sln ./
COPY src/Credo.BlobStorage.TestApi/Credo.BlobStorage.TestApi.csproj src/Credo.BlobStorage.TestApi/
COPY src/Credo.BlobStorage.Client/Credo.BlobStorage.Client.csproj src/Credo.BlobStorage.Client/

# Restore dependencies
RUN dotnet restore src/Credo.BlobStorage.TestApi/Credo.BlobStorage.TestApi.csproj

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/Credo.BlobStorage.TestApi
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Credo.BlobStorage.TestApi.dll"]
