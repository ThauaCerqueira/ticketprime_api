# TicketPrime — Pontos de Melhoria Técnica

> Auditoria realizada em Maio/2026. Classificação por prioridade: **Crítico → Alto → Médio → Baixo**.

---

## 🔴 CRÍTICO — Corrigir antes de ir para produção

### 1. Segredo JWT no histórico do Git
**Arquivo:** `src/appsettings.Development.json`

A chave `TicketPrimeDev_SuperSecretKey_32CharsMinimum_2024!` foi commitada e fica no histórico mesmo após remoção. Qualquer pessoa com acesso ao repositório pode forjar tokens JWT.

**Solução:**
- Remover o secret do histórico com `git filter-repo --path src/appsettings.Development.json --invert-paths` ou BFG Repo-Cleaner
- Usar `dotnet user-secrets` em desenvolvimento
- Em produção, carregar via variável de ambiente ou Azure Key Vault / AWS Secrets Manager

---

### 2. Endpoints de cupom sem autenticação
**Arquivo:** `src/Controllers/CupomController.cs`

Os endpoints `GET /api/cupom` (listar todos) e `GET /api/cupom/{codigo}` estão públicos. Qualquer pessoa pode enumerar todos os códigos de cupom válidos.

**Solução:**
- Adicionar `[Authorize(Roles = "ADMIN")]` em `Listar()` e `ObterPorCodigo()`

---

### 3. Operações multi-passo sem transação explícita
**Arquivo:** `src/Service/EventoService.cs`, `src/Service/ReservaService.cs`

Criação de evento: insere na tabela `Eventos`, depois `TiposIngresso`, depois `Lotes` — em chamadas separadas. Se a segunda ou terceira falhar, ficam registros órfãos no banco.

O mesmo ocorre no cancelamento de reserva: atualiza status + incrementa capacidade + envia email em passos separados.

**Solução:**
- Envolver operações em `IDbTransaction` via Dapper: `connection.BeginTransaction()`
- Passar a transação como parâmetro para os repositórios
- Commit apenas ao final; rollback em qualquer exceção

---

### 4. Validação da string de conexão na inicialização
**Arquivo:** `src/Program.cs`, `src/appsettings.json`

`ConnectionStrings:DefaultConnection` está como string vazia no `appsettings.json`. A aplicação sobe sem erro e só falha na primeira query ao banco, sem mensagem clara.

**Solução:**
```csharp
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
```

---

### 5. Timeout ausente nas queries Dapper
**Arquivos:** todos em `src/Infrastructure/Repository/`

Nenhuma chamada Dapper define `commandTimeout`. Uma query lenta pode segurar a conexão indefinidamente e travar o pool.

**Solução:**
```csharp
await connection.QueryAsync<T>(sql, parameters, commandTimeout: 30);
```
Definir `commandTimeout: 30` (segundos) como padrão em todas as chamadas.

---

## 🟠 ALTO — Corrigir antes da próxima release

### 6. Índices faltando no banco de dados
**Arquivo:** `db/script.sql`

Colunas muito consultadas sem índice causam table scans e degradam performance com volume:

```sql
-- Filtragem de reservas por status (ativo/cancelado)
CREATE NONCLUSTERED INDEX [IX_Reservas_Status_DataCompra]
  ON [Reservas]([Status]) INCLUDE ([DataCompra], [UsuarioCpf]);

-- Listagem de eventos disponíveis
CREATE NONCLUSTERED INDEX [IX_Eventos_Status_DataEvento]
  ON [Eventos]([Status], [DataEvento]) INCLUDE ([Id], [Nome]);

-- Validação de cupom na compra
CREATE NONCLUSTERED INDEX [IX_Cupons_Expiracao]
  ON [Cupons]([DataExpiracao], [TotalUsado], [LimiteUsos]);

-- Trilha de auditoria por usuário
CREATE NONCLUSTERED INDEX [IX_AuditLog_UsuarioCpf_Timestamp]
  ON [AuditLog]([UsuarioCpf], [Timestamp] DESC);

-- Busca de usuário verificado por email
CREATE NONCLUSTERED INDEX [IX_Usuarios_Email_Verificado]
  ON [Usuarios]([Email]) WHERE [EmailVerificado] = 1;
```

---

### 7. Validação de DTOs com FluentValidation
**Arquivos:** `src/Controllers/`, `src/Service/`

Validações estão espalhadas nos serviços com `if` inline e exceções. Dificulta reutilização e testes.

**Solução:**
- Instalar `FluentValidation.AspNetCore`
- Criar validators em `src/Validators/`: `CreateEventDtoValidator`, `PurchaseTicketDtoValidator`, `CreateUserDtoValidator`
- Registrar com `builder.Services.AddValidatorsFromAssemblyContaining<Program>()`
- Remover validações duplicadas dos serviços

---

### 8. Arredondamento em preço de meia-entrada
**Arquivo:** `src/Service/ReservaService.cs`

```csharp
precoBase = ehMeiaEntrada ? ticketType.Preco / 2 : ticketType.Preco;
```

`DECIMAL(18,2) / 2` pode resultar em 49.995, arredondado de forma imprevisível dependendo do contexto.

**Solução:**
```csharp
precoBase = ehMeiaEntrada
    ? Math.Round(ticketType.Preco / 2, 2, MidpointRounding.AwayFromZero)
    : ticketType.Preco;
```

---

### 9. Sem política de retry para serviços externos
**Arquivos:** `src/Service/SmtpEmailService.cs`, `src/Service/MercadoPagoPaymentGateway.cs`

Se o envio de email falhar (ex: SMTP temporariamente fora), a reserva é concluída mas o cliente nunca recebe confirmação. Não há retry.

**Solução:**
- Adicionar **Polly** para retry com backoff exponencial em chamadas externas:
```csharp
Policy.Handle<SmtpException>()
      .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))
```
- Para email: considerar fila assíncrona (ex: canal `Channel<T>` em memória ou tabela `EmailQueue` no banco)

---

### 10. Sem chave de idempotência no gateway de pagamento
**Arquivo:** `src/Service/MercadoPagoPaymentGateway.cs`

Uma requisição de pagamento que sofre timeout pode ser reenviada e cobrar o cliente duas vezes.

**Solução:**
- Gerar `idempotency_key = Guid.NewGuid().ToString()` por tentativa de compra
- Armazenar na tabela `Reservas` antes de chamar o gateway
- Reenviar a mesma chave em retentativas

---

### 11. Webhook de confirmação de pagamento não implementado
**Arquivo:** `src/Controllers/` (endpoint ausente)

O MercadoPago notifica o resultado do pagamento via webhook assíncrono. Sem ele, o sistema não sabe se um pagamento PIX foi efetivamente confirmado.

**Solução:**
- Criar `POST /api/pagamento/webhook` (sem autenticação JWT, com validação de assinatura HMAC do MercadoPago)
- Atualizar status da reserva de `Pendente` → `Ativa` ou `Falhou` com base no evento recebido
- Assinar webhook URL no painel do MercadoPago

---

### 12. Container Docker rodando como root
**Arquivos:** `src/Dockerfile`, `ui/TicketPrime.Web/Dockerfile`

Ambos os Dockerfiles não definem `USER`, então o processo roda como root. Se houver exploit no app, o atacante tem root no container.

**Solução:**
```dockerfile
RUN adduser --disabled-password --gecos "" appuser
USER appuser
```
Adicionar antes do `ENTRYPOINT` em ambos os Dockerfiles.

---

## 🟡 MÉDIO — Melhorias de qualidade e manutenibilidade

### 13. HSTS ativo em ambiente de desenvolvimento
**Arquivo:** `src/Program.cs`

`app.UseHsts()` é chamado em todos os ambientes. Em desenvolvimento/staging, o header HSTS pode forçar HTTPS e quebrar testes locais com HTTP.

**Solução:**
```csharp
if (app.Environment.IsProduction())
    app.UseHsts();
```

---

### 14. Falta UNIQUE CONSTRAINT no email do usuário
**Arquivo:** `db/script.sql`

Há um índice em `Usuarios.Email`, mas não um `CONSTRAINT UNIQUE`. O banco não impede dois usuários com o mesmo email em cenários de concorrência extrema.

**Solução:**
```sql
ALTER TABLE [Usuarios]
  ADD CONSTRAINT [UQ_Usuarios_Email] UNIQUE ([Email]);
```

---

### 15. CHECK CONSTRAINT faltando em Cupons
**Arquivo:** `db/script.sql`

Não há verificação no banco que impeça `TotalUsado > LimiteUsos` (quando `LimiteUsos > 0`).

**Solução:**
```sql
ALTER TABLE [Cupons]
  ADD CONSTRAINT [CK_Cupons_TotalUsado]
  CHECK ([LimiteUsos] = 0 OR [TotalUsado] <= [LimiteUsos]);
```

---

### 16. docker-compose com ASPNETCORE_ENVIRONMENT = Production localmente
**Arquivo:** `docker-compose.yml`

O arquivo principal define `ASPNETCORE_ENVIRONMENT: Production`, então rodar `docker compose up` localmente desativa o Swagger e aplica restrições de produção.

**Solução:**
- Criar `docker-compose.override.yml` com `ASPNETCORE_ENVIRONMENT: Development` para uso local
- Manter `docker-compose.yml` com Production apenas para o ambiente de deploy

---

### 17. Sem limites de memória/CPU nos containers
**Arquivo:** `docker-compose.yml`

Um container com vazamento de memória pode consumir toda RAM do host e derrubar os demais.

**Solução:**
```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 512M
    reservations:
      memory: 256M
```

---

### 18. Migrations formais ausentes
**Arquivo:** `db/script.sql`

O script SQL é monolítico. Quando o banco já existe em produção, rodar o script do zero não funciona. Os `IF NOT EXISTS` no final são paliativos, não migrations.

**Solução:**
- Adotar **DbUp** (biblioteca .NET leve, sem EF):
  - Scripts numerados em `db/migrations/V001__initial.sql`, `V002__add_capacidade_restante.sql`
  - DbUp roda na inicialização apenas os scripts ainda não aplicados
  - Mantém histórico auditável no banco (tabela `SchemaVersions`)

---

### 19. Logs sem correlation ID
**Arquivo:** `src/Program.cs`

Logs não têm identificador de requisição. Impossível rastrear uma compra específica entre múltiplos logs.

**Solução:**
```csharp
app.Use(async (context, next) => {
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    using var scope = logger.BeginScope(new { RequestId = requestId });
    context.Response.Headers["X-Request-Id"] = requestId;
    await next();
});
```

---

### 20. Enumeração de perfis de organizador sem rate limit
**Arquivo:** `src/Controllers/PublicController.cs`

`GET /api/public/organizador/{slug}` não tem rate limiting. Um atacante pode iterar slugs e mapear todos os organizadores cadastrados.

**Solução:**
- Aplicar a política `geral` de rate limiting (60 req/min) explicitamente no endpoint
- Ou criar política específica: 30 req/min por IP para endpoints de perfil público

---

## 🔵 BAIXO — Nice to have / Débito técnico

### 21. Cobertura de testes insuficiente nos repositórios
Os repositórios em `src/Infrastructure/Repository/` não têm testes unitários. São a camada de maior risco (SQL direto).

**Recomendação:** Usar Testcontainers para subir SQL Server em container durante testes de integração dos repositórios.

---

### 22. Detalhes de infraestrutura no endpoint /health público
**Arquivo:** `src/Controllers/HealthController.cs`

`GET /health` retorna status do banco sem autenticação. Vaza informações de infraestrutura.

**Recomendação:** Separar em dois endpoints:
- `GET /health` — público, retorna apenas `{"status":"ok"}`
- `GET /health/detail` — `[Authorize(Roles = "ADMIN")]`, retorna detalhes do banco, memória, etc.

---

### 23. Verificação de autenticidade dos documentos de meia-entrada
**Arquivo:** `src/Service/LocalMeiaEntradaStorageService.cs`

Documentos são armazenados mas não verificados. Um usuário pode enviar qualquer imagem como comprovante.

**Recomendação:** Implementar fluxo de aprovação manual pelo organizador do evento, com notificação por email após aprovação/rejeição.

---

### 24. Sem versão semântica na API
Não há versionamento de API (`/api/v1/...`). Mudanças breaking afetam todos os clientes sem transição.

**Recomendação:** Adicionar `Asp.Versioning.Http` e prefixar rotas com `/api/v1/`.

---

### 25. Imagens Docker sem scan de vulnerabilidades
**Arquivos:** `src/Dockerfile`, `ui/TicketPrime.Web/Dockerfile`

As imagens base `mcr.microsoft.com/dotnet/aspnet:10.0` podem ter CVEs conhecidas.

**Recomendação:**
- Adicionar step de scan no CI com `trivy image ticketprime-api:latest`
- Considerar imagem Alpine para reduzir superfície de ataque

---

## Resumo por prioridade

| # | Item | Prioridade | Arquivo principal |
|---|------|-----------|------------------|
| 1 | Segredo JWT no histórico do Git | 🔴 Crítico | `appsettings.Development.json` |
| 2 | Endpoints de cupom sem auth | 🔴 Crítico | `CupomController.cs` |
| 3 | Operações sem transação explícita | 🔴 Crítico | `EventoService.cs`, `ReservaService.cs` |
| 4 | Validação da connection string | 🔴 Crítico | `Program.cs` |
| 5 | Timeout ausente nas queries | 🔴 Crítico | `Repository/*.cs` |
| 6 | Índices faltando | 🟠 Alto | `db/script.sql` |
| 7 | FluentValidation | 🟠 Alto | `Controllers/`, `Service/` |
| 8 | Arredondamento meia-entrada | 🟠 Alto | `ReservaService.cs` |
| 9 | Retry para serviços externos | 🟠 Alto | `SmtpEmailService.cs`, `MercadoPago*.cs` |
| 10 | Idempotência no pagamento | 🟠 Alto | `MercadoPagoPaymentGateway.cs` |
| 11 | Webhook de pagamento ausente | 🟠 Alto | (novo controller) |
| 12 | Container rodando como root | 🟠 Alto | `Dockerfile` (ambos) |
| 13 | HSTS em desenvolvimento | 🟡 Médio | `Program.cs` |
| 14 | UNIQUE CONSTRAINT no email | 🟡 Médio | `db/script.sql` |
| 15 | CHECK CONSTRAINT em Cupons | 🟡 Médio | `db/script.sql` |
| 16 | docker-compose com Production local | 🟡 Médio | `docker-compose.yml` |
| 17 | Sem limites de memória nos containers | 🟡 Médio | `docker-compose.yml` |
| 18 | Migrations formais ausentes | 🟡 Médio | `db/` |
| 19 | Logs sem correlation ID | 🟡 Médio | `Program.cs` |
| 20 | Enumeração de organizadores | 🟡 Médio | `PublicController.cs` |
| 21 | Cobertura dos repositórios | 🔵 Baixo | `tests/` |
| 22 | /health vazando infra | 🔵 Baixo | `HealthController.cs` |
| 23 | Aprovação de meia-entrada | 🔵 Baixo | `LocalMeiaEntradaStorageService.cs` |
| 24 | Sem versionamento de API | 🔵 Baixo | `Program.cs` |
| 25 | Sem scan de vulnerabilidades nas imagens | 🔵 Baixo | `Dockerfile` (ambos) |

---

*Documento gerado com base em auditoria técnica do repositório TicketPrime API — branch `GiuliaDiva123`.*
