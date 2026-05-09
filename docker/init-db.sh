#!/bin/bash
# Inicia o SQL Server em background e executa o script de inicialização
# após o servidor estar pronto.

set -e

# Inicia o SQL Server em background (processo principal)
/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

echo "[init-db] Aguardando SQL Server inicializar..."

# Tenta conectar por até 60 segundos (30 tentativas × 2s)
for i in $(seq 1 30); do
    /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "${SA_PASSWORD}" \
        -Q "SELECT 1" -C \
        > /dev/null 2>&1

    if [ $? -eq 0 ]; then
        echo "[init-db] SQL Server pronto após ${i} tentativa(s)."
        break
    fi

    echo "[init-db] Tentativa ${i}/30: ainda não disponível. Aguardando 2s..."
    sleep 2
done

# Executa o script de inicialização do banco
echo "[init-db] Executando script de inicialização..."
/opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${SA_PASSWORD}" \
    -i /scripts/init.sql -C

echo "[init-db] Script executado com sucesso."

# Aguarda o processo do SQL Server (mantém o container vivo)
wait $MSSQL_PID
