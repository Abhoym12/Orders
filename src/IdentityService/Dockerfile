FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/IdentityService/IdentityService/IdentityService.csproj", "src/IdentityService/IdentityService/"]

# Restore dependencies
RUN dotnet restore "src/IdentityService/IdentityService/IdentityService.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/IdentityService/IdentityService"
RUN dotnet build "IdentityService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IdentityService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IdentityService.dll"]
