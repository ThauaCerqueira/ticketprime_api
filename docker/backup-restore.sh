#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
#  TicketPrime — Restore do MinIO S3
#
#  Lista backups disponíveis no MinIO e restaura o banco.
#  Uso:
#    docker compose exec backup /backup-restore.sh --list
#    docker compose exec backup /backup-restore.sh --latest
#    docker compose exec backup /backup-restore.sh --restore daily/20240516_120000.bak
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

# ── Config ─────────────────────────────────────────────────────────────────
DB_HOST="${DB_HOST:-sqlserver}"
DB_PORT="${DB_PORT:-1433}"
SA_PASSWORD="${SA_PASSWORD:?SA_PASSWORD is required}"
DB_NAME="${DB_NAME:-TicketPrime}"

S3_ENDPOINT="${S3_ENDPOINT:-http://minio:9000}"
S3_ACCESS_KEY="${S3_ACCESS_KEY:-ticketprime-backup}"
S3_SECRET_KEY="${S3_SECRET_KEY:?S3_SECRET_KEY is required}"
S3_BUCKET="${S3_BUCKET:-ticketprime}"

mc alias set ticketprime-s3 "${S3_ENDPOINT}" "${S3_ACCESS_KEY}" "${S3_SECRET_KEY}" > /dev/null

ALIAS_PATH="ticketprime-s3/${S3_BUCKET}"

list_backups() {
  echo "═══════════════════════════════════════════════════════════════"
  echo "  📋 Backups disponíveis no MinIO"
  echo "═══════════════════════════════════════════════════════════════"
  for prefix in daily weekly monthly; do
    echo ""
    echo "  📁 ${prefix}/"
    if mc ls "${ALIAS_PATH}/${prefix}/" 2>/dev/null | grep -q .; then
      mc ls "${ALIAS_PATH}/${prefix}/" 2>/dev/null | while read -r line; do
        echo "    ${line}"
      done
    else
      echo "    (vazio)"
    fi
  done
}

restore_backup() {
  local S3_KEY="$1"
  local LOCAL_FILE="/tmp/restore_$(basename "${S3_KEY}")"

  echo "[$(date -Iseconds)] 🔄 Baixando s3://${S3_BUCKET}/${S3_KEY}..."
  mc cp "${ALIAS_PATH}/${S3_KEY}" "${LOCAL_FILE}"

  if [ ! -f "${LOCAL_FILE}" ]; then
    echo "[$(date -Iseconds)] ❌ Erro ao baixar arquivo"
    exit 1
  fi

  RESTORE_SIZE=$(du -h "${LOCAL_FILE}" | cut -f1)
  echo "[$(date -Iseconds)] ✅ Baixado (${RESTORE_SIZE})"

  echo "[$(date -Iseconds)] 🔄 Restaurando ${DB_NAME} a partir do backup..."
  echo "    Arquivo: ${LOCAL_FILE}"

  # Descobre nomes lógicos do data/log file dentro do .bak
  FILELIST=$(/opt/mssql-tools18/bin/sqlcmd \
    -S "${DB_HOST},${DB_PORT}" -U sa -P "${SA_PASSWORD}" -C \
    -Q "RESTORE FILELISTONLY FROM DISK = N'${LOCAL_FILE}';" 2>/dev/null)

  DATA_NAME=$(echo "${FILELIST}" | awk 'NR==4{print $1}')
  LOG_NAME=$(echo "${FILELIST}" | awk 'NR==5{print $1}')

  if [ -z "${DATA_NAME}" ]; then
    DATA_NAME="${DB_NAME}"
    LOG_NAME="${DB_NAME}_log"
  fi

  echo "[$(date -Iseconds)] 🔄 Data: ${DATA_NAME} | Log: ${LOG_NAME}"

  /opt/mssql-tools18/bin/sqlcmd \
    -S "${DB_HOST},${DB_PORT}" -U sa -P "${SA_PASSWORD}" -C \
    -Q "
      RESTORE DATABASE [${DB_NAME}]
      FROM DISK = N'${LOCAL_FILE}'
      WITH REPLACE,
           MOVE N'${DATA_NAME}' TO N'/var/opt/mssql/data/${DB_NAME}.mdf',
           MOVE N'${LOG_NAME}' TO N'/var/opt/mssql/data/${DB_NAME}_log.ldf',
           STATS = 10;
    "

  echo "[$(date -Iseconds)] 🧹 Limpando arquivo temporário..."
  rm -f "${LOCAL_FILE}"

  echo ""
  echo "═══════════════════════════════════════════════════════════════"
  echo "  ✅ Restauração concluída!"
  echo "  ☁️  Fonte: s3://${S3_BUCKET}/${S3_KEY}"
  echo "═══════════════════════════════════════════════════════════════"
}

restore_latest() {
  echo "[$(date -Iseconds)] 🔍 Buscando backup mais recente..."

  # Procura o .bak mais recente entre todos os prefixes
  LATEST=$(mc find "${ALIAS_PATH}" --name "*.bak" 2>/dev/null | sort -r | head -1)

  if [ -z "${LATEST}" ]; then
    echo "[$(date -Iseconds)] ❌ Nenhum backup encontrado no MinIO"
    exit 1
  fi

  # Extrai caminho relativo: remove "ticketprime-s3/ticketprime/"
  REL_PATH="${LATEST#ticketprime-s3/${S3_BUCKET}/}"
  echo "[$(date -Iseconds)] ✅ Mais recente: ${REL_PATH}"
  restore_backup "${REL_PATH}"
}

# ── Main ──────────────────────────────────────────────────────────────────
case "${1:---help}" in
  --list)
    list_backups
    ;;
  --latest)
    restore_latest
    ;;
  --restore)
    if [ -z "${2:-}" ]; then
      echo "❌ Uso: $0 --restore <caminho>"
      echo "   Ex: $0 --restore weekly/TicketPrime_20240516_120000.bak"
      exit 1
    fi
    restore_backup "$2"
    ;;
  --help|-h|*)
    echo "Uso: $0 [--list | --latest | --restore <caminho>]"
    echo ""
    echo "  --list                   Lista backups no MinIO"
    echo "  --latest                 Restaura o backup mais recente"
    echo "  --restore <caminho>      Restaura um backup específico"
    echo "                           Ex: --restore daily/20240516_120000.bak"
    ;;
esac
