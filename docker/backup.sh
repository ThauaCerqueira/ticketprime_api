#!/usr/bin/env bash
# ═══════════════════════════════════════════════════════════════════
#  TicketPrime — SQL Server Full Backup (compactado + MinIO S3)
#
#  Estratégia:
#    - Full:    executa 1x/semana (domingo) → prefixo "weekly/"
#    - Diff:    executa diariamente → prefixo "daily/"
#    - Mensal:  1º dia do mês → prefixo "monthly/"
#
#  Compressão: WITH COMPRESSION reduz ~80% do tamanho do .bak
#  Storage:    .bak → /tmp/ → upload para MinIO S3 → deleção local
#  Retenção:   gerenciada pelo Lifecycle Policy do MinIO
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

# ── Configurações (com fallback) ──────────────────────────────────────────────
DB_HOST="${DB_HOST:-sqlserver}"
DB_PORT="${DB_PORT:-1433}"
SA_PASSWORD="${SA_PASSWORD:?SA_PASSWORD is required}"
DB_NAME="${DB_NAME:-TicketPrime}"

# MinIO / S3
S3_ENDPOINT="${S3_ENDPOINT:-http://minio:9000}"
S3_ACCESS_KEY="${S3_ACCESS_KEY:-ticketprime-backup}"
S3_SECRET_KEY="${S3_SECRET_KEY:?S3_SECRET_KEY is required}"
S3_BUCKET="${S3_BUCKET:-ticketprime}"

# ── Determina tipo de backup pela data ────────────────────────────────────────
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
DAY_OF_WEEK=$(date +"%u")        # 1=segunda .. 7=domingo
DAY_OF_MONTH=$(date +"%d")
HOUR=$(date +"%H")

# Estratégia:
#   - 1º dia do mês → monthly (mantido 365 dias)
#   - Domingo       → weekly   (mantido 30 dias)
#   - Dias normais  → daily    (mantido 7 dias)
#
# Retenção automática pelo Lifecycle Policy do MinIO.

if [ "${DAY_OF_MONTH}" = "01" ] && [ "${HOUR}" = "00" ]; then
  BACKUP_TYPE="monthly"
  BACKUP_LABEL="${DB_NAME}-Monthly"
elif [ "${DAY_OF_WEEK}" = "7" ]; then
  BACKUP_TYPE="weekly"
  BACKUP_LABEL="${DB_NAME}-Weekly"
else
  BACKUP_TYPE="daily"
  BACKUP_LABEL="${DB_NAME}-Daily"
fi

BACKUP_FILE="/tmp/${DB_NAME}_${BACKUP_TYPE}_${TIMESTAMP}.bak"
S3_PATH="${S3_BUCKET}/${BACKUP_TYPE}/${DB_NAME}_${TIMESTAMP}.bak"

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  🗄️  TicketPrime Backup"
echo "  📅 $(date -Iseconds)"
echo "  📦 Tipo:      ${BACKUP_TYPE}"
echo "  🗃️  Database:  ${DB_NAME}"
echo "  📏 Compressão: SIM (SQL Server nativa)"
echo "  ☁️  Destino:   MinIO S3 (${S3_BUCKET})"
echo "═══════════════════════════════════════════════════════════════"

# ── 1. Executa BACKUP DATABASE WITH COMPRESSION ──────────────────────────────
echo ""
echo "[$(date -Iseconds)] 🔄 Iniciando backup..."

/opt/mssql-tools18/bin/sqlcmd \
  -S "${DB_HOST},${DB_PORT}" \
  -U sa \
  -P "${SA_PASSWORD}" \
  -C \
  -Q "
    BACKUP DATABASE [${DB_NAME}]
    TO DISK = N'${BACKUP_FILE}'
    WITH
      COMPRESSION,                                  -- Reduz ~80% do tamanho
      NOFORMAT,
      NOINIT,
      NAME = N'${BACKUP_LABEL}',
      SKIP,
      NOREWIND,
      NOUNLOAD,
      STATS = 10;                                   -- Mostra progresso a cada 10%
  "

# Verifica se o backup foi gerado
if [ ! -f "${BACKUP_FILE}" ]; then
  echo "[$(date -Iseconds)] ❌ ERRO: Arquivo de backup não foi criado!"
  exit 1
fi

BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
echo "[$(date -Iseconds)] ✅ Backup concluído: ${BACKUP_FILE} (${BACKUP_SIZE})"

# ── 2. Upload para MinIO S3 ──────────────────────────────────────────────────
echo "[$(date -Iseconds)] ☁️  Enviando para MinIO S3..."

mc alias set ticketprime-s3 "${S3_ENDPOINT}" "${S3_ACCESS_KEY}" "${S3_SECRET_KEY}" > /dev/null

if mc cp "${BACKUP_FILE}" "ticketprime-s3/${S3_PATH}"; then
  echo "[$(date -Iseconds)] ✅ Upload concluído: s3://${S3_PATH}"
else
  echo "[$(date -Iseconds)] ❌ ERRO no upload para MinIO!"
  rm -f "${BACKUP_FILE}"
  exit 1
fi

# ── 3. Remove .bak local (preserva espaço do container) ─────────────────────
echo "[$(date -Iseconds)] 🧹 Removendo .bak local..."
rm -f "${BACKUP_FILE}"
echo "[$(date -Iseconds)] ✅ Backup local removido"

# ── 4. Lista backups no MinIO para referência ────────────────────────────────
echo ""
echo "[$(date -Iseconds)] 📋 Backups em ${S3_BUCKET}/${BACKUP_TYPE}/:"
mc ls "ticketprime-s3/${S3_BUCKET}/${BACKUP_TYPE}/" 2>/dev/null | tail -5

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  ✅ Backup ${BACKUP_TYPE} concluído com sucesso!"
echo "  ☁️  ${S3_ENDPOINT}/${S3_PATH}"
echo "  📏 Tamanho: ${BACKUP_SIZE} (comprimido)"
echo "  🗑️  Retenção: ${BACKUP_TYPE} → lifecycle policy"
echo "═══════════════════════════════════════════════════════════════"
