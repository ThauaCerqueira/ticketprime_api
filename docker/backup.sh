#!/usr/bin/env bash
# TicketPrime — SQL Server automated backup script
# Executed by the backup service in docker-compose (scheduled via cron).
# Backups are stored in /backups/<date>/<dbname>_<timestamp>.bak
# Retention: keeps last 7 days of backups.

set -euo pipefail

DB_HOST="${DB_HOST:-sqlserver}"
DB_PORT="${DB_PORT:-1433}"
SA_PASSWORD="${SA_PASSWORD:?SA_PASSWORD is required}"
DB_NAME="${DB_NAME:-TicketPrime}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
DATE_DIR=$(date +"%Y%m%d")
BACKUP_PATH="${BACKUP_DIR}/${DATE_DIR}"
BACKUP_FILE="${BACKUP_PATH}/${DB_NAME}_${TIMESTAMP}.bak"

mkdir -p "${BACKUP_PATH}"

echo "[$(date -Iseconds)] Starting backup of ${DB_NAME} → ${BACKUP_FILE}"

/opt/mssql-tools18/bin/sqlcmd \
  -S "${DB_HOST},${DB_PORT}" \
  -U sa \
  -P "${SA_PASSWORD}" \
  -C \
  -Q "BACKUP DATABASE [${DB_NAME}]
      TO DISK = N'${BACKUP_FILE}'
      WITH NOFORMAT, NOINIT, NAME = N'${DB_NAME}-Full',
           SKIP, NOREWIND, NOUNLOAD, STATS = 10;"

echo "[$(date -Iseconds)] Backup complete: ${BACKUP_FILE}"

# Remove backups older than RETENTION_DAYS
find "${BACKUP_DIR}" -name "*.bak" -mtime "+${RETENTION_DAYS}" -delete
echo "[$(date -Iseconds)] Old backups purged (retention: ${RETENTION_DAYS} days)"
