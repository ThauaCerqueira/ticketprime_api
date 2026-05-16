#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
#  Script de inicialização do HashiCorp Vault (open-source)
#  Uso: docker compose exec vault sh /vault-setup.sh
# ═══════════════════════════════════════════════════════════════════

set -e

echo "⏳ Aguardando Vault iniciar..."
until vault status > /dev/null 2>&1; do
  sleep 1
done

echo "✓ Vault está respondendo"

# ── 1. Inicializa o Vault (apenas na primeira execução) ────────────
# Se o Vault já foi inicializado, pula esta etapa
if ! vault status | grep -q "Initialized"; then
  echo "🔑 Inicializando Vault (primeira execução)..."

  # Inicializa com 1 chave de desbloqueio (dev — NÃO usar em produção)
  vault operator init -key-shares=1 -key-threshold=1 > /tmp/vault-keys.txt

  UNSEAL_KEY=$(grep "Unseal Key 1" /tmp/vault-keys.txt | awk '{print $NF}')
  ROOT_TOKEN=$(grep "Initial Root Token" /tmp/vault-keys.txt | awk '{print $NF}')

  echo "✓ Vault inicializado!"
  echo "⚠  Guarde estas informações em local seguro:"
  echo "   Unseal Key: $UNSEAL_KEY"
  echo "   Root Token: $ROOT_TOKEN"

  # Salva em arquivo (apenas para dev!)
  echo "$UNSEAL_KEY" > /vault/data/unseal-key.txt
  echo "$ROOT_TOKEN" > /vault/data/root-token.txt
fi

# ── 2. Desbloqueia o Vault ─────────────────────────────────────────
if vault status | grep -q "Sealed"; then
  echo "🔓 Desbloqueando Vault..."
  UNSEAL_KEY=$(cat /vault/data/unseal-key.txt 2>/dev/null || echo "")
  if [ -n "$UNSEAL_KEY" ]; then
    vault operator unseal "$UNSEAL_KEY"
    echo "✓ Vault desbloqueado!"
  else
    echo "❌ Chave de desbloqueio não encontrada!"
    echo "   Execute manualmente: vault operator unseal <chave>"
    exit 1
  fi
fi

# ── 3. Login com root token ────────────────────────────────────────
ROOT_TOKEN=$(cat /vault/data/root-token.txt 2>/dev/null || echo "")
if [ -n "$ROOT_TOKEN" ]; then
  vault login "$ROOT_TOKEN" > /dev/null
fi

# ── 4. Habilita o KV v2 secrets engine ─────────────────────────────
if ! vault secrets list | grep -q "^secret/"; then
  echo "📦 Habilitando KV v2 em secret/"
  vault secrets enable -path=secret -version=2 kv
  echo "✓ KV v2 habilitado!"
fi

# ── 5. Cria a chave cripto da TicketPrime ─────────────────────────
# Gera uma chave ECDH P-256 e salva no Vault
echo "🔐 Gerando chave ECDH para TicketPrime..."
PRIVATE_KEY=$(openssl ecparam -name prime256v1 -genkey -noout 2>/dev/null | base64 -w0)

if [ -n "$PRIVATE_KEY" ]; then
  vault kv put secret/ticketprime/crypto PrivateKeyBase64="$PRIVATE_KEY"
  echo "✓ Chave cripto salva em secret/ticketprime/crypto"
else
  echo "⚠  openssl não disponível. Pulando geração automática."
  echo "   Crie manualmente:"
  echo "   vault kv put secret/ticketprime/crypto PrivateKeyBase64=\"<base64>\""
fi

# ── 6. Gera chave mestra para criptografia Pix ────────────────────
echo "🔐 Gerando chave mestra Pix (AES-256)..."
PIX_KEY=$(openssl rand -base64 32 2>/dev/null)

if [ -n "$PIX_KEY" ]; then
  vault kv patch secret/ticketprime/crypto PixMasterKey="$PIX_KEY"
  echo "✓ Chave Pix salva em secret/ticketprime/crypto (PixMasterKey)"
else
  echo "⚠  openssl não disponível. Pulando geração automática."
  echo "   Crie manualmente:"
  echo "   vault kv patch secret/ticketprime/crypto PixMasterKey=\"<base64>\""
fi

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo "  ✅ Vault pronto!"
echo "  Endpoint: http://vault:8200"
echo "  Root Token: $(cat /vault/data/root-token.txt 2>/dev/null)"
echo "═══════════════════════════════════════════════════════════════"

# Token para a TicketPrime API
echo ""
echo "Para gerar um token para a API, execute:"
echo "  docker compose exec vault vault token create -policy=default -ttl=720h"
