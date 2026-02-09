FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/VibeSQL.Server/VibeSQL.Server.csproj", "src/VibeSQL.Server/"]
COPY ["src/VibeSQL.Core/VibeSQL.Core.csproj", "src/VibeSQL.Core/"]
RUN dotnet restore "src/VibeSQL.Server/VibeSQL.Server.csproj"
COPY . .
WORKDIR "/src/src/VibeSQL.Server"
RUN dotnet build "VibeSQL.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VibeSQL.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VibeSQL.Server.dll"]
