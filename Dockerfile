FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY src/Shivakala.Web/*.csproj src/Shivakala.Web/
COPY src/Shivakala.Core/*.csproj src/Shivakala.Core/
COPY src/Shivakala.Infrastructure/*.csproj src/Shivakala.Infrastructure/
COPY src/Shivakala.PostgresMigrations/*.csproj src/Shivakala.PostgresMigrations/
COPY src/Shivakala.SqlServerMigrations/*.csproj src/Shivakala.SqlServerMigrations/

# Restore
RUN dotnet restore src/Shivakala.Web/Shivakala.Web.csproj

# Copy everything
COPY . .

# Publish
RUN dotnet publish src/Shivakala.Web/Shivakala.Web.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Create App_Data directory
RUN mkdir -p /app/App_Data

# Environment variables (Render will override PORT at runtime)
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000

ENTRYPOINT ["dotnet", "Shivakala.Web.dll"]
