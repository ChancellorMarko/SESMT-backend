# ─────────────────────────────────────────────────────────────────────────────
# 1. Base runtime (ASP.NET Core 10)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Usuário não-root padrão das imagens .NET 8+ (UID 1654)
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ─────────────────────────────────────────────────────────────────────────────
# 2. Build (SDK 10)
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copia apenas os .csproj para aproveitar cache de restore
COPY ["SESMT.API/SESMT.API.csproj", "SESMT.API/"]
COPY ["SESMT.Application/SESMT.Application.csproj", "SESMT.Application/"]
COPY ["SESMT.Domain/SESMT.Domain.csproj", "SESMT.Domain/"]
COPY ["SESMT.Infrastructure/SESMT.Infrastructure.csproj", "SESMT.Infrastructure/"]

RUN dotnet restore "./SESMT.API/SESMT.API.csproj"

# Copia o restante do código
COPY . .
WORKDIR "/src/SESMT.API"

RUN dotnet build "./SESMT.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ─────────────────────────────────────────────────────────────────────────────
# 3. Publish
# ─────────────────────────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SESMT.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ─────────────────────────────────────────────────────────────────────────────
# 4. Final (runtime)
# ─────────────────────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SESMT.API.dll"]