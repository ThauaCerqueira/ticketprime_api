# 🚀 Scripts de Automação - TicketPrime

Estes scripts automatizam a execução do projeto com Docker, Backend e Frontend.

## � Estrutura de Scripts

```
scripts/
├── dev/                 # Scripts de Desenvolvimento
│   ├── start.ps1        # Inicia todos os serviços (Docker + API + Frontend)
│   ├── start-db.ps1     # Inicia apenas o Banco de Dados (SQL Server)
│   ├── start-backend.ps1# Inicia apenas o Backend (API)
│   ├── start-frontend.ps1# Inicia apenas o Frontend (Blazor)
│   └── stop.ps1         # Para todos os processos
│
└── setup/               # Scripts de Setup/Configuração
    ├── setup-database.ps1  # Setup do banco (PowerShell)
    └── setup-database.csx  # Setup do banco (C# Script)
```

---

## 🎯 Início Rápido

### ✨ Opção 1: Tudo de Uma Vez (Recomendado)

Inicia todos os serviços em 3 janelas separadas:

```powershell
cd .\scripts\dev
.\start.ps1
```

**Abre automaticamente:**
- 🐳 Docker Compose (SQL Server) → localhost:1433
- 🔌 Backend (API) → http://localhost:5164/swagger
- 🎨 Frontend (Blazor) → http://localhost:5194

---

### 🔧 Opção 2: Componentes Individuais

Execute cada componente em um terminal separado:

**Terminal 1 - Banco de Dados:**
```powershell
.\scripts\dev\start-db.ps1
```

**Terminal 2 - Backend (API):**
```powershell
.\scripts\dev\start-backend.ps1
```

**Terminal 3 - Frontend (Blazor):**
```powershell
.\scripts\dev\start-frontend.ps1
```

---

## 🛑 Encerrar Todos os Serviços

```powershell
.\scripts\dev\stop.ps1
```

Ou use o parâmetro `-Kill`:
```powershell
.\scripts\dev\start.ps1 -Kill
```

---

## 🗄️ Configuração do Banco de Dados

### Setup Inicial (PowerShell)
```powershell
.\scripts\setup\setup-database.ps1
```

### Setup Inicial (C# Script)
```powershell
dotnet script .\scripts\setup\setup-database.csx
```

**Variáveis de Ambiente (Opcionais):**
```powershell
$env:DB_SERVER = "localhost,1433"
$env:DB_NAME = "master"
$env:DB_USER = "sa"
$env:DB_PASSWORD = "TicketPrime@2024!"
```

---

## 🔐 Credenciais de Teste

| Perfil | Campo | Valor |
|--------|-------|-------|
| 👑 Administrador | CPF | `00000000191` |
| 👑 Administrador | Senha | `admin123` (temporária — trocar no 1º login) |
| 🧑 Cliente teste | CPF | `66301020022` |
| 🧑 Cliente teste | Senha | `Test@1234` |

---

## 🌐 Acessos Rápidos

| Serviço | URL |
|---------|-----|
| 📱 Frontend | http://localhost:5194 |
| 🔌 API Swagger | http://localhost:5164/swagger |
| 💾 SQL Server | localhost:1433 |

---

## 📝 Notas Importantes

✅ Os scripts devem ser executados do diretório correto
✅ Se tiver erro de permissão, execute PowerShell como **Administrador**
✅ Docker Compose deve estar instalado
✅ Cada script abre automaticamente a porta necessária
✅ Use `Ctrl+C` para parar um serviço individual

---

## 🐛 Solução de Problemas

**Erro: "docker-compose: comando não encontrado"**
- Instale [Docker Desktop](https://www.docker.com/products/docker-desktop)

**Erro: "dotnet: comando não encontrado"**
- Instale [.NET 10 SDK](https://dotnet.microsoft.com/download)

**Erro: "Porta já em uso (5164, 5194)"**
- Execute: `.\scripts\dev\stop.ps1`
- Ou: `netstat -ano | findstr :5164` para identificar o processo

**Arquivo bloqueado ao compilar**
- Execute: `.\scripts\dev\stop.ps1`
- Limpe: `Remove-Item -Path src\bin, src\obj -Recurse -Force`

**Erro de conexão com SQL Server**
- Verifique se Docker está rodando: `docker ps`
- Aguarde 30 segundos após iniciar `start-db.ps1`
- Verifique a senha em `docker-compose.yml`

---

## 🚀 Dicas Avançadas

### Compilação sem executar
```powershell
cd .\src
dotnet build
```

### Build Release
```powershell
cd .\src
dotnet publish -c Release
```

### Limpar cache de compilação
```powershell
Remove-Item -Path .\src\bin, .\src\obj, .\ui\TicketPrime.Web\bin, .\ui\TicketPrime.Web\obj -Recurse -Force
```

### Ver logs do Docker
```powershell
docker-compose logs -f mssql
```

---
