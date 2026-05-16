#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
#  TicketPrime — MinIO Initialization Script
#  Cria buckets e configura lifecycle policies automaticamente.
#  Executado UMA vez na criação do container.
# ═══════════════════════════════════════════════════════════════════

set -euo pipefail

# ── Config ─────────────────────────────────────────────────────────────
MINIO_ROOT_USER="${MINIO_ROOT_USER:-admin}"
MINIO_ROOT_PASSWORD="${MINIO_ROOT_PASSWORD:?MINIO_ROOT_PASSWORD is required}"
MINIO_ENDPOINT="${MINIO_ENDPOINT:-minio:9000}"
BUCKET_NAME="${BUCKET_NAME:-ticketprime}"
ALIAS="local"

echo "[minio-init] ⏳ Aguardando MinIO iniciar..."

# Aguarda MinIO responder
for i in $(seq 1 30); do
  if mc alias set "${ALIAS}" "http://${MINIO_ENDPOINT}" "${MINIO_ROOT_USER}" "${MINIO_ROOT_PASSWORD}" > /dev/null 2>&1; then
    echo "[minio-init] ✅ MinIO pronto na tentativa ${i}"
    break
  fi
  if [ "${i}" -eq 30 ]; then
    echo "[minio-init] ❌ MinIO não respondeu após 30 tentativas"
    exit 1
  fi
  sleep 2
done

# ── Cria bucket ────────────────────────────────────────────────────────
if mc ls "${ALIAS}/${BUCKET_NAME}" > /dev/null 2>&1; then
  echo "[minio-init] ✅ Bucket '${BUCKET_NAME}' já existe"
else
  echo "[minio-init] 📦 Criando bucket '${BUCKET_NAME}'..."
  mc mb "${ALIAS}/${BUCKET_NAME}"
  echo "[minio-init] ✅ Bucket '${BUCKET_NAME}' criado"
fi

# ── Lifecycle policy ───────────────────────────────────────────────────
# Regras automáticas de retenção:
#   - daily/*        → expira em 7 dias
#   - weekly/*       → expira em 30 dias
#   - monthly/*      → expira em 365 dias
echo "[minio-init] 📋 Configurando lifecycle policy..."

cat > /tmp/lifecycle.json << 'LFEOF'
{
  "Rules": [
    {
      "ID": "expire-daily-after-7d",
      "Status": "Enabled",
      "Filter": {"Prefix": "daily/"},
      "Expiration": {"Days": 7}
    },
    {
      "ID": "expire-weekly-after-30d",
      "Status": "Enabled",
      "Filter": {"Prefix": "weekly/"},
      "Expiration": {"Days": 30}
    },
    {
      "ID": "expire-monthly-after-365d",
      "Status": "Enabled",
      "Filter": {"Prefix": "monthly/"},
      "Expiration": {"Days": 365}
    }
  ]
}
LFEOF

mc ilm import "${ALIAS}/${BUCKET_NAME}" < /tmp/lifecycle.json
echo "[minio-init] ✅ Lifecycle policy aplicada"

# ── Versions / Lock (proteção contra deleção acidental) ────────────────
echo "[minio-init] 🔒 Habilitando versionamento..."
mc version enable "${ALIAS}/${BUCKET_NAME}"
echo "[minio-init] ✅ Versionamento ativado"

# ── Cria usuário de serviço para backup (princípio do menor privilégio) ─
if ! mc admin user info "${ALIAS}" ticketprime-backup > /dev/null 2>&1; then
  echo "[minio-init] 👤 Criando usuário 'ticketprime-backup'..."
  mc admin user add "${ALIAS}" "ticketprime-backup" "${MINIO_BACKUP_SECRET:?MINIO_BACKUP_SECRET is required}"
  echo "[minio-init] ✅ Usuário 'ticketprime-backup' criado"
fi

# ── Policy de acesso restrito para o usuário de backup ─────────────────
cat > /tmp/backup-policy.json << 'POLICYEOF'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject",
        "s3:ListBucket",
        "s3:DeleteObject"
      ],
      "Resource": [
        "arn:aws:s3:::ticketprime/*",
        "arn:aws:s3:::ticketprime"
      ]
    }
  ]
}
POLICYEOF

mc admin policy create "${ALIAS}" ticketprime-backup-policy /tmp/backup-policy.json
mc admin policy set "${ALIAS}" ticketprime-backup-policy user="ticketprime-backup"
echo "[minio-init] ✅ Policy restrita aplicada ao usuário 'ticketprime-backup'"

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  ✅ MinIO inicializado com sucesso!"
echo "  📦 Bucket:       ${BUCKET_NAME}"
echo "  👤 User backup:  ticketprime-backup"
echo "  📋 Lifecycle:    daily→7d | weekly→30d | monthly→365d"
echo "  🔒 Versionado:   sim"
echo "═══════════════════════════════════════════════════════════════"
