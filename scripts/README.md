# 📁 Scripts de Automação - TicketPrime

Pasta com scripts PowerShell para iniciar, parar e configurar a aplicação TicketPrime.

## 🎯 Estrutura

### 📂 `dev/` - Desenvolvimento e Execução

Scripts para iniciar e parar os serviços durante desenvolvimento.

| Script | Descrição |
|--------|-----------|
| `start.ps1` | Inicia **tudo** (Docker + Backend + Frontend) em 3 janelas |
| `start-db.ps1` | Inicia apenas o **Banco de Dados** (SQL Server) |
| `start-backend.ps1` | Inicia apenas o **Backend** (API) |
| `start-frontend.ps1` | Inicia apenas o **Frontend** (Blazor) |
| `stop.ps1` | Para **todos** os processos |

### 📂 `setup/` - Configuração Inicial

Scripts para configuração do banco de dados.

| Script | Descrição |
|--------|-----------|
| `setup-database.ps1` | Executa setup do BD em PowerShell |
| `setup-database.csx` | Executa setup do BD em C# Script |

---

## 🚀 Uso Rápido

### Iniciar Tudo
```powershell
cd .\scripts\dev
.\start.ps1
```

### Parar Tudo
```powershell
.\scripts\dev\stop.ps1
# ou
.\scripts\dev\start.ps1 -Kill
```

### Componentes Individuais
```powershell
.\scripts\dev\start-db.ps1      # Banco
.\scripts\dev\start-backend.ps1  # API
.\scripts\dev\start-frontend.ps1 # Frontend
```

---

## 🔗 Acessos

- **Frontend:** http://localhost:5194
- **API Swagger:** http://localhost:5164/swagger
- **SQL Server:** localhost:1433

---

## 🔐 Credenciais Padrão

```
CPF: 00000000000
Senha: admin123
```

---

## ⚠️ Pré-requisitos

- ✅ Docker Desktop instalado
- ✅ .NET 10 SDK instalado
- ✅ PowerShell 5.0+
- ✅ Permissões de administrador (para Docker)

---

## 📖 Documentação Completa

Veja [AUTOMATION_SCRIPTS.md](../AUTOMATION_SCRIPTS.md) na raiz do projeto.
