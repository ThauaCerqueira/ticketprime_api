# ── Estágio 1: build ──────────────────────────────────────────────────────────
# O contexto de build deve ser a raiz do repositório (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Restaura dependências (camada de cache separada)
# A UI referencia src/src.csproj, então ambos os projetos são copiados primeiro
COPY src/src.csproj src/
COPY ui/TicketPrime.Web/TicketPrime.Web.csproj ui/TicketPrime.Web/
RUN dotnet restore ui/TicketPrime.Web/TicketPrime.Web.csproj

# Copia o restante e publica
COPY src/ src/
COPY ui/TicketPrime.Web/ ui/TicketPrime.Web/
RUN dotnet publish ui/TicketPrime.Web/TicketPrime.Web.csproj -c Release -o /publish --no-restore

# ── Estágio 2: runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /publish .

# .NET 8+ escuta na porta 8080 por padrão em containers
EXPOSE 8080

ENTRYPOINT ["dotnet", "TicketPrime.Web.dll"]
