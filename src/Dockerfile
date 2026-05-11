# ── Estágio 1: build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Restaura dependências (camada de cache separada)
COPY src/src.csproj src/
RUN dotnet restore src/src.csproj

# Copia o restante e publica
COPY src/ src/
RUN dotnet publish src/src.csproj -c Release -o /publish --no-restore

# ── Estágio 2: runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /publish .
# Copia o script de banco para que InitializeDatabase encontre db/script.sql em runtime
COPY db/script.sql db/script.sql

# .NET 8+ escuta na porta 8080 por padrão em containers
EXPOSE 8080

ENTRYPOINT ["dotnet", "src.dll"]
