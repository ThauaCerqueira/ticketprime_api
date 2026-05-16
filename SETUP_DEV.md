# 🚀 Setup para Desenvolvimento — TicketPrime

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

## 1. Primeiro clone

```bash
git clone <url-do-repositorio>
cd ticketprime_api
```

## 2. Configurar User Secrets (OBRIGATÓRIO)

As credenciais sensíveis (senha do banco, chave JWT) **não estão no repositório**.
Você precisa configurá-las localmente:

```bash
cd src
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=TicketPrime;User Id=sa;Password=<senha_do_SA>;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key" "<sua_chave_jwt_de_no_minimo_32_caracteres>"
```

### Onde encontrar as senhas?

- A senha do SA está no cofre de senhas da equipe (HashiCorp Vault ou Bitwarden)
- Se for o primeiro setup, gere uma nova senha e rode:
  ```sql
  ALTER LOGIN sa WITH PASSWORD = '<nova_senha>';
  ```

## 3. Subir o banco

```bash
# Sobe o SQL Server via Docker
docker compose up -d sqlserver

# Verificar se está saudável
docker compose ps sqlserver

# Executar script de inicialização (se banco vazio)
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "<senha>" -C -i /scripts/init.sql
```

## 4. Rodar a API

```bash
cd src
dotnet run
```

Swagger disponível em: **http://localhost:5164/swagger**

## 5. Rodar os testes

```bash
cd tests
dotnet test
```

## 6. (Opcional) Subir Vault

```bash
docker compose -f docker-compose.yml -f docker-compose.vault.yml up -d vault
docker compose exec vault sh /vault-setup.sh
```

## 7. (Recomendado) Ativar Backup Automático com MinIO

O projeto usa **MinIO** (S3-compatible, open-source) para armazenar backups do SQL Server com segurança, sem ocupar espaço no volume do banco.

### Subir MinIO + Backup

```bash
# 1. Sobe o MinIO
docker compose up -d minio

# 2. Inicializa bucket, lifecycle e usuario de backup (uma vez)
docker compose --profile setup run --rm minio-init

# 3. Sobe o backup agendado (a cada 12h)
docker compose --profile backup up -d backup
```

### Acessar Console MinIO

- **URL:** http://localhost:9001
- **Login:** `admin` / senha definida em `MINIO_ROOT_PASSWORD` no `.env`

### Restaurar um Backup

```bash
# Listar backups disponíveis
docker compose exec backup /backup-restore.sh --list

# Restaurar o mais recente
docker compose exec backup /backup-restore.sh --latest

# Restaurar um específico
docker compose exec backup /backup-restore.sh --restore weekly/TicketPrime_20240516_120000.bak
```

### Estratégia de Backup

| Tipo | Frequência | Prefixo | Retenção |
|------|-----------|---------|----------|
| **Daily** | Todo dia útil | `daily/` | 7 dias |
| **Weekly** (Full) | Todo domingo | `weekly/` | 30 dias |
| **Monthly** (Full) | 1º dia do mês | `monthly/` | 365 dias |

- **Compressão:** `WITH COMPRESSION` reduz ~80% do tamanho do `.bak`
- **Storage:** `.bak` fica em `/tmp/` no container e é enviado ao MinIO, depois removido — **zero espaço permanente ocupado**
- **Retenção:** Gerenciada automaticamente pelo Lifecycle Policy do MinIO

---

## 📧 E-mails Transacionais

A TicketPrime envia e-mails para:
- Confirmação de cadastro (link de verificação)
- Confirmação de compra
- Cancelamento de ingresso
- Redefinição de senha
- Notificação de vaga na fila de espera

**Em desenvolvimento (sem SMTP):** Os e-mails são exibidos no console do backend (`ConsoleEmailService`).
Para testar o fluxo completo, configure SMTP via variáveis de ambiente:

```bash
$env:EmailSettings__SmtpHost = "smtp.exemplo.com"
$env:EmailSettings__SmtpPort = "587"
$env:EmailSettings__SmtpUsername = "seu@email.com"
$env:EmailSettings__SmtpPassword = "sua_senha"
$env:EmailSettings__FromEmail = "nao-responder@ticketprime.com.br"
```

> ⚠️ **Fila de espera:** A notificação de vaga disponível na fila de espera depende de SMTP
> configurado. Sem SMTP, o sistema registra a tentativa nos logs, mas o e-mail não é enviado.

## ⚠️ Segurança

| Item | Status |
|------|--------|
| `appsettings.Development.json` | ✅ No `.gitignore` — nunca commitar |
| Senha SA | 🔒 User Secrets / Vault |
| Chave JWT | 🔒 User Secrets / Vault |
| Chave Pix | 🔒 Criptografada em repouso |
| Backups (MinIO) | 🔒 Armazenamento local S3, sem dados trafegando para internet |
