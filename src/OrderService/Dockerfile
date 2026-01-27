FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/OrderService/OrderService.Api/OrderService.Api/OrderService.Api.csproj", "src/OrderService/OrderService.Api/OrderService.Api/"]
COPY ["src/OrderService/OrderService.Manager/OrderService.Manager/OrderService.Manager.csproj", "src/OrderService/OrderService.Manager/OrderService.Manager/"]
COPY ["src/OrderService/OrderService.Engine/OrderService.Engine/OrderService.Engine.csproj", "src/OrderService/OrderService.Engine/OrderService.Engine/"]
COPY ["src/OrderService/OrderService.DataAccess/OrderService.DataAccess/OrderService.DataAccess.csproj", "src/OrderService/OrderService.DataAccess/OrderService.DataAccess/"]
COPY ["src/Shared/Contracts/Shared.Contracts/Shared.Contracts.csproj", "src/Shared/Contracts/Shared.Contracts/"]
COPY ["src/Shared/Messaging/Shared.Messaging/Shared.Messaging.csproj", "src/Shared/Messaging/Shared.Messaging/"]

# Restore dependencies
RUN dotnet restore "src/OrderService/OrderService.Api/OrderService.Api/OrderService.Api.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/OrderService/OrderService.Api/OrderService.Api"
RUN dotnet build "OrderService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]
