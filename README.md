# 🎟️ TicketPrime

Sistema de venda de ingressos rápido, seguro e escalável, desenvolvido como projeto oficial da disciplina de Engenharia de Software.

## 👥 Equipe

| Nome | Matrícula |
|---|---|
| Thauã Cerqueira | 06010400 |
| Felipe Dário | 06009691 |
| Pedro Freitas | 06009656 |
| Pedro Henrique Alves | 06003335 |
| Gabriel | 06009870 |


---

## 🚀 Como Executar

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server com instância `SQLEXPRESS`
- [SQL Server Management Studio (SSMS)](https://aka.ms/ssmsfullsetup)

### Banco de Dados

```bash
# Abra o arquivo /db/script.sql no SSMS e execute (F5)
# Isso vai criar o banco TicketPrime, as 4 tabelas e o usuário admin padrão
```

### API (Backend)

```bash
# 1. Clone o repositório
git clone https://github.com/ThauaCerqueira/ticketprime_api.git
cd ticketprime_api

# 2. Entre na pasta da API
cd src

# 3. Execute
dotnet run
```

Swagger disponível em: **http://localhost:5164/swagger**

### Frontend (Blazor)

```bash
cd ui/TicketPrime.Web
dotnet run
```

Acesse no navegador: **http://localhost:5194**

### Testes

```bash
cd tests
dotnet test
```

### Comandos úteis

```bash
dotnet build        # Compilar
dotnet clean        # Limpar build
dotnet restore      # Restaurar pacotes
```

---

## 🔐 Contas de Teste

| Tipo | CPF | Senha |
|---|---|---|
| Administrador | `00000000191` | Gerada automaticamente no 1º deploy (veja logs) |
| Cliente | Cadastre-se em `/cadastro-user` | — |

> ⚠️ **Segurança:** A senha do administrador é gerada aleatoriamente na primeira execução da aplicação e exibida nos logs. A senha `admin123` (hardcoded no script SQL) é automaticamente substituída. **NUNCA** use a senha padrão em produção.

---

## 📁 Estrutura do Projeto

```
ticketprime_api/
├── src/                  # API REST em C# com Dapper + Controllers
│   ├── Controllers/      # Controladores da API (16 controllers)
│   ├── DTOs/             # Objetos de transferência de dados com validação
│   ├── Infrastructure/   # DbConnectionFactory, Repositories, Interfaces, Redis, Caching
│   ├── Models/           # Modelos de dados (Evento, Reserva, Usuario, Cupom, etc.)
│   ├── Service/          # Serviços de negócio (Auth, Evento, Reserva, Cupom, Crypto, Pagamento)
│   ├── Properties/       # Configuração de launchSettings
│   └── Program.cs        # Pipeline de middleware, DI, segurança, OpenTelemetry
├── ui/                   # Frontend Blazor WebAssembly
│   ├── TicketPrime.Web/           # Projeto host (App.razor, assets, js)
│   ├── TicketPrime.Web.Client/    # Componentes Blazor WASM (páginas, shared, layout)
│   └── TicketPrime.Web.Shared/    # Modelos compartilhados (DTOs entre frontend e backend)
├── db/                   # Scripts SQL + Migrações versionadas (V*__*.sql)
│   └── migrations/       # Migrações incrementais (DbUp/Flyway)
├── docker/               # Dockerfiles, nginx.conf, scripts de inicialização
├── docs/                 # Documentação (ADR, requisitos, operação)
├── scripts/              # Scripts de setup e utilidades
├── tests/                # Testes xUnit com Moq + testes E2E (Playwright)
├── plans/                # Planos técnicos detalhados
└── .github/workflows/    # CI/CD com GitHub Actions (build + test + docker)
```

---

## 📋 Histórias de Usuário Implementadas

| ID | Papel | Descrição | Status |
|---|---|---|---|
| US-01 | Usuário | Como usuário, quero cadastrar uma conta com CPF e senha para acessar a plataforma | ✅ |
| US-02 | Usuário | Como usuário, quero comprar ingressos para eventos disponíveis garantindo minha vaga | ✅ |
| US-03 | Usuário | Como usuário, quero cancelar um ingresso comprado para liberar minha vaga | ✅ |
| US-04 | Usuário | Como usuário, quero visualizar todos os meus ingressos comprados | ✅ |
| US-05 | Administrador | Como administrador, quero cadastrar eventos com nome, data, capacidade e preço | ✅ |
| US-06 | Administrador | Como administrador, quero cadastrar cupons de desconto com código e percentual | ✅ |

## ✅ Critérios de Aceitação

| ID | Papel | Critério |
|---|---|---|
| US-01 | Usuário | CPF único por cadastro — retorna erro 400 se CPF já existir |
| US-02 | Usuário | Bloqueia compra se evento lotado ou data já passou |
| US-03 | Usuário | Cancela reserva e devolve vaga ao evento automaticamente |
| US-04 | Usuário | Lista ingressos com nome do evento, data e preço via INNER JOIN |
| US-05 | Administrador | Valida capacidade > 0 e data futura antes de salvar |
| US-06 | Administrador | Valida código único e desconto entre 1% e 100% |

---

## ⚙️ Tecnologias

- **Blazor WebAssembly** — Frontend SPA com C# e MudBlazor UI
- **.NET 10 / C#** — Backend com controllers REST
- **Dapper** — Acesso ao banco com SQL puro e parâmetros `@`
- **SQL Server** — Banco de dados relacional
- **JWT** — Autenticação via Bearer Token (header + httpOnly cookie) com blacklist
- **BCrypt** — Hash de senhas com work factor 11+
- **Mercado Pago SDK** — Tokenização segura de cartões (PCI-DSS via iframes)
- **Web Crypto API** — E2E encryption de fotos (ECDH P-256 + AES-GCM-256)
- **FluentValidation** — Validação de formulários no frontend e backend
- **xUnit + Moq** — Testes unitários + Playwright para testes E2E
- **Rate Limiting** — 4 políticas por usuário (login, compra, escrita, geral)
- **Redis** — Cache distribuído (fallback para memória local)
- **OpenTelemetry + Prometheus** — Métricas e observabilidade
- **MinIO** — Armazenamento S3-compatible para backups e arquivos
- **HashiCorp Vault** — Gerenciamento de secrets (opcional)
- **GitHub Actions** — CI/CD com build, testes e Docker

---

## 🔄 Metodologia

Modelo **Incremental e Iterativo**, com entregas organizadas por funcionalidade e validação contínua das regras de negócio.

---

## ⚠️ Riscos Identificados

| Risco | Mitigação |
|---|---|
| Superlotação de evento | Controle de capacidade com decremento atômico no banco + UPDLOCK |
| Compra de ingresso com data expirada | Validação de `DataEvento > DateTime.Now` antes do INSERT |
| CPF duplicado no cadastro | Verificação prévia no banco antes de inserir |
| XSS em cadastro | Sanitização de nome com `SanitizarNome()` + HTML encoding do Blazor |
| Fraude com cupom abaixo do valor mínimo | Validação do `ValorMinimoRegra` antes de aplicar desconto |
| SQL Injection | Todas as queries usam parâmetros Dapper com `@` |
| Colisão de código de ingresso | Unique constraint filtrada `UX_Reservas_CodigoIngresso` |
| Performance em consultas de reserva | Índices `IX_Reservas_UsuarioCpf` e `IX_Reservas_EventoId` |
