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
# ═══════════════════════════════════════════════════════════════════
# SEGURANÇA: script.sql NÃO é copiado para a imagem de produção.
#   Em produção, o banco já deve estar criado e migrado.
#   InitializeDatabase() só deve rodar em Development.
#   Use migrations versionadas (DbUp/Flyway) para produção.
# ═══════════════════════════════════════════════════════════════════

# Executa como usuário não-root para reduzir superfície de ataque
RUN useradd --system --create-home --shell /sbin/nologin appuser

# Instala curl para healthcheck (como root, depois volta para appuser)
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*
USER appuser

# .NET 8+ escuta na porta 8080 por padrão em containers
EXPOSE 8080

ENTRYPOINT ["dotnet", "src.dll"]
