# ── Estágio 1: build ──────────────────────────────────────────────────────────
# O contexto de build deve ser a raiz do repositório (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Restaura dependências (camada de cache separada)
# A UI referencia src/src.csproj, TicketPrime.Web.Client e TicketPrime.Web.Shared
COPY src/src.csproj src/
COPY ui/TicketPrime.Web/TicketPrime.Web.csproj ui/TicketPrime.Web/
COPY ui/TicketPrime.Web.Client/TicketPrime.Web.Client.csproj ui/TicketPrime.Web.Client/
COPY ui/TicketPrime.Web.Shared/TicketPrime.Web.Shared.csproj ui/TicketPrime.Web.Shared/
RUN dotnet restore ui/TicketPrime.Web/TicketPrime.Web.csproj

# Copia o restante e publica
COPY src/ src/
COPY ui/ ui/

# Remove appsettings duplicados da src/ (conflito com os da UI ao publicar)
RUN rm -f src/appsettings.json src/appsettings.Development.json

RUN dotnet publish ui/TicketPrime.Web/TicketPrime.Web.csproj -c Release -o /publish --no-restore

# ── Estágio 2: runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /publish .

# .NET 8+ escuta na porta 8080 por padrão em containers
EXPOSE 8080

# Executa como usuário não-root para reduzir superfície de ataque
RUN useradd --system --create-home --shell /sbin/nologin appuser

# Instala curl para healthcheck (como root, depois volta para appuser)
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*
USER appuser

ENTRYPOINT ["dotnet", "TicketPrime.Web.dll"]
