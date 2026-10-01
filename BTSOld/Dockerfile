# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS base
WORKDIR /app
EXPOSE 5000

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BatteryTestingSystem.csproj", "."]
RUN dotnet restore "./BatteryTestingSystem.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./BatteryTestingSystem.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BatteryTestingSystem.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
# Expose ports for HTTP and UDP as per workflow
EXPOSE 9999/tcp
EXPOSE 10000/udp
EXPOSE 10001/udp
EXPOSE 10002/udp
EXPOSE 10003/udp

# Set environment variables (can be overridden at runtime)
ENV ASPNETCORE_URLS=http://+:5000

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BatteryTestingSystem.dll"]