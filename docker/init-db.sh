#!/bin/bash
# Inicia o SQL Server em background e executa o script de inicialização
# após o servidor estar pronto.
# NOTA: set -e removido intencionalmente — o sqlcmd falha durante o warmup
# do SQL Server e não deve derrubar o container.

# Inicia o SQL Server em background (processo principal)
/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

echo "[init-db] Aguardando SQL Server inicializar..."

# Tenta conectar por até 120 segundos (60 tentativas × 2s)
READY=0
for i in $(seq 1 60); do
    /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "${SA_PASSWORD}" \
        -Q "SELECT 1" -C -l 2 \
        > /dev/null 2>&1 && READY=1 && break

    echo "[init-db] Tentativa ${i}/60: ainda não disponível. Aguardando 2s..."
    sleep 2
done

if [ "$READY" -eq 1 ]; then
    echo "[init-db] SQL Server pronto. Executando script de inicialização..."
    /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "${SA_PASSWORD}" \
        -i /scripts/init.sql -C
    echo "[init-db] Script executado com sucesso."
else
    echo "[init-db] AVISO: SQL Server não ficou pronto em 120s. Script não executado."
fi

# Aguarda o processo do SQL Server (mantém o container vivo)
wait $MSSQL_PID
