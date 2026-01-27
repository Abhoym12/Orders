FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/ApiGateway/ApiGateway/ApiGateway.csproj", "src/ApiGateway/ApiGateway/"]

# Restore dependencies
RUN dotnet restore "src/ApiGateway/ApiGateway/ApiGateway.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/ApiGateway/ApiGateway"
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["src/ApiGateway/ApiGateway/ocelot.json", "./"]
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
