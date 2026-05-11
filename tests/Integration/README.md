# 🧪 Testes de Integração — TicketPrime

## 📋 Pré-requisitos

- Docker Desktop (para rodar o SQL Server)
- .NET 10 SDK

## 🚀 Como Executar

### 1. Suba o SQL Server de teste

```bash
# Opção A: Usar o mesmo banco do docker-compose principal (porta 1433)
docker compose up -d sqlserver

# Opção B: Usar o banco de teste isolado (porta 1434 — não conflita com dev)
docker compose -f docker-compose.test.yml up -d
```

### 2. Execute os testes de integração

```bash
# Opção A: Usando connection string default (docker-compose principal)
dotnet test --filter "Category=Integration"

# Opção B: Usando banco de teste na porta 1434
$env:TEST_CONNECTION_STRING = "Server=localhost,1434;Database=TicketPrime;User Id=sa;Password=TicketPrime@2024!;TrustServerCertificate=True;"
dotnet test --filter "Category=Integration"

# Opção C: Usando um SQL Server existente qualquer
$env:TEST_CONNECTION_STRING = "Server=meu-servidor,1433;Database=TicketPrime;User Id=...;Password=...;"
dotnet test --filter "Category=Integration"
```

### 3. Execute todos os testes (unitários + integração)

```bash
dotnet test
```

## 🔬 O que é testado

### Repositórios (CRUD básico)

| Repositório | Operações testadas |
|------------|-------------------|
| `UsuarioRepository` | Criar, ObterPorCpf, ObterPorEmail, AtualizarSenha, ConfirmarEmail, SalvarTokenVerificacaoEmail |
| `EventoRepository` | Adicionar, ObterPorId, ObterTodos (paginação), DiminuirCapacidade, AumentarCapacidade, AtualizarStatus, BuscarDisponiveis, Deletar |
| `CupomRepository` | Criar, ObterPorCodigo, IncrementarUso, Listar, Deletar |
| `ReservaRepository` | Criar, ListarPorUsuario, Cancelar, ObterDetalhadaPorId, ObterPorCodigoIngresso, ContarReservas |

### Fluxos completos

- **Compra com cupom**: cria usuário → cria evento → cria cupom → reserva com desconto → incrementa uso do cupom → diminui capacidade
- **Cancelamento com restauração**: cria reserva → diminui capacidade → cancela → aumenta capacidade de volta
- **Segurança**: cancelamento de outro usuário deve falhar

## 📁 Estrutura

```
tests/Integration/
├── README.md                    # Este arquivo
├── IntegrationTestFixture.cs    # Fixture xUnit (IAsyncLifetime) com transação rollback
└── DatabaseIntegrationTests.cs  # Testes de integração propriamente ditos
```

## ⚙️ Funcionamento da Fixture

O `IntegrationTestFixture`:

1. Lê a connection string da env var `TEST_CONNECTION_STRING` (fallback para `localhost,1433`)
2. Abre uma conexão com o SQL Server e inicia uma transação
3. Disponibiliza todos os repositórios via propriedades
4. Ao final de cada teste, **reverte a transação** — garantindo isolamento total

> ⚠️ **Importante:** Como cada repositório abre sua própria conexão (não há UnitOfWork),
> as transações da fixture servem apenas como salvaguarda. Cada operação de repositório
> é atômica e persiste independentemente. O rollback da fixture garante que, mesmo
> que dados sejam escritos, o banco volte ao estado original após cada teste.

## 🏷️ Categorias

- `Category=Integration` — filtro para rodar apenas testes de integração
- Testes unitários existentes não têm esta tag e continuam rodando isoladamente
