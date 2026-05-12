# 🧪 Guia Completo: Configurar e Executar Testes — TicketPrime

## Sumário

1. [Pré-requisitos](#1-pré-requisitos)
2. [Parte A — Integration Tests (SQL Server)](#parte-a--integration-tests-sql-server)
3. [Parte B — E2E Tests (Playwright)](#parte-b--e2e-tests-playwright)
4. [Parte C — Executar Todos os Testes](#parte-c--executar-todos-os-testes)
5. [Solução de Problemas](#solução-de-problemas)

---

## 1. Pré-requisitos

| Item | Como verificar |
|------|---------------|
| ✅ **Docker Desktop** | `docker --version` → `26.1.1` ✔ |
| ✅ **.NET 10 SDK** | `dotnet --version` → `10.0.203` ✔ |
| ✅ **Node.js** (para Playwright) | `node --version` |
| ✅ **Git Bash** (opcional) | `git --version` |

> Se faltar Node.js, instale em: https://nodejs.org (LTS)

---

## Parte A — Integration Tests (SQL Server)

### Visão Geral

Os testes de integração precisam de um **SQL Server real**. O projeto oferece dois containers Docker prontos:

| Container | Porta | Uso |
|-----------|-------|-----|
| `sqlserver` (docker-compose.yml) | `1433` | Desenvolvimento normal |
| `sqlserver-test` (docker-compose.test.yml) | `1434` | **Recomendado para testes** (não conflita com dev) |

### Passo a Passo

#### 🔹 Passo A1: Suba o container do SQL Server

Abra o **PowerShell** ou **CMD** na raiz do projeto (`c:/Users/giuli/Downloads/ticketprime_api`) e execute:

```powershell
# Opção recomendada: container isolado na porta 1434
docker compose -f docker-compose.test.yml up -d
```

> 💡 **Explicação:** O `-f` especifica um arquivo compose diferente. O `-d` roda em background (detached).
> O container se chama `ticketprime_sqlserver_test` e já executa o script `db/script.sql` automaticamente.

**Alternativa** (se quiser usar o mesmo banco do dev, porta 1433):

```powershell
# Define a senha primeiro (obrigatória no docker-compose principal)
$env:SA_PASSWORD = "TicketPrime@2024!"
docker compose up -d sqlserver
```

#### 🔹 Passo A2: Verifique se o SQL Server está pronto

```powershell
docker ps --filter "name=ticketprime_sqlserver_test"
```

A saída deve mostrar o container com status `Up` e `(healthy)`.

Para ver os logs:

```powershell
docker logs ticketprime_sqlserver_test --tail 20
```

#### 🔹 Passo A3: Execute os testes de integração

```powershell
# Define a connection string apontando para a porta 1434 (container de teste)
$env:TEST_CONNECTION_STRING = "Server=localhost,1434;Database=TicketPrime;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;"

# Roda APENAS os testes de integração
dotnet test --filter "Category=Integration"
```

**Resultado esperado:** ✅ Todos os testes de integração passam (eles testam repositórios como `UsuarioRepository`, `EventoRepository`, fluxos de compra com cupom, cancelamento, etc.)

#### 🔹 Passo A4: Para parar o container (quando terminar)

```powershell
docker compose -f docker-compose.test.yml down
```

---

## Parte B — E2E Tests (Playwright)

### Visão Geral

Os testes E2E usam **Playwright** (automação de navegador) para testar a UI do Blazor Server. Eles precisam de:

1. Playwright **instalado como biblioteca .NET** (já está no `TicketPrime.E2E.csproj` ✅)
2. **Browsers do Playwright** baixados (Chromium, Firefox, WebKit)
3. A **aplicação completa rodando** (API + Frontend)

### Passo a Passo

#### 🔹 Passo B1: Instale os browsers do Playwright

Dentro da pasta do projeto E2E:

```powershell
cd tests/E2E

# Instala os browsers (Chromium, Firefox, WebKit) — ~300MB
pwsh -Command "dotnet tool install --global Microsoft.Playwright.CLI 2>$null; playwright install chromium"
```

> ⚠️ Se o comando acima falhar, use a alternativa:

```powershell
# Alternativa via PowerShell direto
cd tests/E2E
dotnet build
pwsh -Command "& {[System.Environment]::SetEnvironmentVariable('PLAYWRIGHT_BROWSERS_PATH', '0'); & dotnet tool install --global Microsoft.Playwright.CLI 2>$null; & playwright install chromium}"
```

**O que isso faz?**
- Instala a ferramenta global `playwright` (CLI)
- Baixa o **Chromium** (~130MB) — o suficiente para os testes atuais
- Os browsers ficam em `%USERPROFILE%\.cache\ms-playwright\`

> 💡 **Dica:** Se quiser instalar todos os navegadores (Firefox + WebKit também), troque `chromium` por `--with-deps` ou execute `playwright install` (sem argumentos).

#### 🔹 Passo B2: Verifique se os browsers foram instalados

```powershell
playwright --version
# Deve mostrar algo como: Version 1.52.0

# Lista os browsers instalados
playwright install --dry-run
```

#### 🔹 Passo B3: Inicie a aplicação completa (API + Frontend)

Os testes E2E precisam da aplicação rodando. Use os scripts de desenvolvimento:

```powershell
# Terminal 1: Primeiro o SQL Server (se já não estiver rodando do Passo A1)
cd c:/Users/giuli/Downloads/ticketprime_api
$env:SA_PASSWORD = "TicketPrime@2024!"
docker compose up -d sqlserver

# Terminal 2: Depois a API
cd c:/Users/giuli/Downloads/ticketprime_api/src
$env:SA_PASSWORD = "TicketPrime@2024!"
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=TicketPrime;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;"
dotnet run --urls "https://localhost:5001;http://localhost:5000"
```

> ⏳ Aguarde a API iniciar (cerca de 10-15s). Quando aparecer _"Now listening on..."_ está pronta.

```powershell
# Terminal 3: Depois o Frontend Blazor
cd c:/Users/giuli/Downloads/ticketprime_api/ui/TicketPrime.Web
dotnet run --urls "https://localhost:5002;http://localhost:5001"
```

> 💡 **Alternativa mais rápida:** Use os scripts prontos de desenvolvimento:

```powershell
# Inicia TUDO (DB + API + Frontend) de uma vez
.\scripts\dev\start.ps1
```

#### 🔹 Passo B4: Execute os testes E2E

Com a aplicação rodando, em um **novo terminal**:

```powershell
cd c:/Users/giuli/Downloads/ticketprime_api

# Define a URL base (padrão é https://localhost)
$env:TICKETPRIME_BASE_URL = "https://localhost:5002"  # Ajuste se sua porta for diferente

# Roda APENAS os testes E2E
dotnet test tests/E2E/TicketPrime.E2E.csproj
```

**Resultado esperado:** ✅ Navegador Chromium abre automaticamente, executa os cenários de UI (compra sem login, página de detalhes do evento) e os testes passam.

---

## Parte C — Executar Todos os Testes

### 1. Apenas testes unitários (não precisam de banco)

```powershell
dotnet test --filter "Category!=Integration&Category!=E2E"
```

### 2. Testes unitários + integração

```powershell
# Com SQL Server rodando (Passo A1)
$env:TEST_CONNECTION_STRING = "Server=localhost,1434;Database=TicketPrime;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;"
dotnet test --filter "Category!=E2E"
```

### 3. Tudo (unitários + integração + E2E)

```powershell
# SQL Server rodando + Browsers instalados + API + Frontend rodando
$env:TEST_CONNECTION_STRING = "Server=localhost,1434;Database=TicketPrime;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;"
$env:TICKETPRIME_BASE_URL = "https://localhost:5002"
dotnet test
```

---

## Solução de Problemas

### 🔴 Integration Tests falham com "Cannot connect to SQL Server"

| Causa | Solução |
|-------|---------|
| Container não está rodando | `docker compose -f docker-compose.test.yml up -d` |
| Container ainda inicializando | Aguarde 30s e tente novamente |
| Porta conflitante | Verifique com `netstat -ano \| findstr :1434` |
| Senha errada na env var | Confirme `$env:TEST_CONNECTION_STRING` |

### 🔴 E2E Tests falham com "Playwright executable not found"

```powershell
# Reinstala os browsers
playwright install chromium --force
```

### 🔴 Playwright CLI não encontrada

```powershell
# Caminho onde o dotnet tool instala globalmente
$env:Path += ";$env:USERPROFILE\.dotnet\tools"
# ou reinstale:
dotnet tool install --global Microsoft.Playwright.CLI
```

### 🔴 Erro "No events available" nos testes E2E

Os testes E2E precisam de pelo menos **um evento** no banco. Use o script de seed:

```powershell
.\criar_evento.ps1
```

### 🔴 Porta já em uso (API ou Frontend)

```powershell
# Mude a porta ao rodar
dotnet run --urls "https://localhost:5003;http://localhost:5002"
# E ajuste TICKETPRIME_BASE_URL
$env:TICKETPRIME_BASE_URL = "https://localhost:5003"
```

---

## Referências

- [`tests/Integration/README.md`](tests/Integration/README.md) — Documentação oficial dos testes de integração
- [`tests/Integration/IntegrationTestFixture.cs`](tests/Integration/IntegrationTestFixture.cs:1) — Fixture que gerencia a conexão com SQL Server
- [`tests/Integration/DatabaseIntegrationTests.cs`](tests/Integration/DatabaseIntegrationTests.cs) — Testes de integração
- [`tests/E2E/CheckoutFlowTests.cs`](tests/E2E/CheckoutFlowTests.cs:1) — Testes E2E de fluxo de compra
- [`docker-compose.test.yml`](docker-compose.test.yml:1) — Container SQL Server isolado para testes
- [`docker-compose.yml`](docker-compose.yml:1) — Stack completa
