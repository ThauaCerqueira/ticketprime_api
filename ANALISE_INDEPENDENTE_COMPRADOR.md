# Análise Independente do Comprador — TicketPrime

> **Contexto:** Análise independente profunda do TicketPrime como **comprador potencial**. Foram lidos **todos os arquivos do projeto** (~50+ arquivos, ~13.000+ linhas de código). Esta análise foca em **gaps críticos não capturados** pela análise existente (`ANALISE_COMPRADOR_TICKETPRIME.md`), além de reavaliar a nota geral.
>
> **⚠️ Conclusão principal:** A análise existente atribui 9.2/10 e afirma que o projeto está "pronto para produção". **Discordo.** Minha avaliação independente identifica **gaps fundamentais** que impedem o uso real como bilheteria. Estimo o projeto em **~6.0/10** — tecnicamente sólido em segurança/arquitetura, mas **funcionalmente incompleto** para o domínio de bilheteria.

---

## Sumário

1. [🔴 GAPS FUNCIONAIS CRÍTICOS — O sistema não vende ingressos](#1--gaps-funcionais-criticos--o-sistema-nao-vende-ingressos)
2. [🔴 SEGURANÇA — Riscos residuais não endereçados](#2--seguranca--riscos-residuais-nao-enderecados)
3. [🟠 ARQUITETURA — Problemas estruturais não resolvidos](#3--arquitetura--problemas-estruturais-nao-resolvidos)
4. [🟡 FRONTEND — Problemas de UX e qualidade](#4--frontend--problemas-de-ux-e-qualidade)
5. [🔵 BACKEND — Bugs e más práticas](#5--backend--bugs-e-mas-praticas)
6. [🟣 DADOS E BANCO — Problemas de schema](#6--dados-e-banco--problemas-de-schema)
7. [⚪ TESTES — O que está sendo testado de fato](#7--testes--o-que-esta-sendo-testado-de-fato)
8. [🟤 DEVOPS — Problemas de deployment](#8--devops--problemas-de-deployment)
9. [⚫ DOCUMENTAÇÃO E OBSERVABILIDADE](#9--documentacao-e-observabilidade)
10. [📊 REAVALIAÇÃO DA NOTA E VEREDITO](#10--reavaliacao-da-nota-e-veredito)

---

## 1. 🔴 GAPS FUNCIONAIS CRÍTICOS — O sistema não vende ingressos

### 1.1 Sem processamento de pagamento — bilheteria sem cobrança 🔴 10/10

**Status:** Não implementado. **GAP GRAVÍSSIMO.**

O TicketPrime é um sistema de **bilheteria** que **não processa pagamentos**. Não há integração com:
- Stripe
- PayPal / Mercado Pago / PagSeguro
- Pix (obrigatório no Brasil)
- Cartão de crédito/débito
- Boleto bancário
- Carteira digital

O fluxo de "compra" em [`ComprarIngressoAsync`](src/Service/ReservaService.cs:20-107) simplesmente:
1. Valida dados do usuário/evento/cupom
2. Decrementa `CapacidadeTotal`
3. Insere uma `Reserva` com `Status = 'Ativa'`

**Não há cobrança, não há confirmação de pagamento, não há estorno.** O ingresso é "vendido" sem qualquer transação financeira real. Uma bilheteria que não cobra não é uma bilheteria — é um sistema de reserva grátis.

**Impacto comercial:** O sistema é inutilizável para qualquer negócio real de venda de ingressos.

**✅ Recomendação:** Implementar integração com gateway de pagamento (Mercado Pago/Pix é obrigatório no Brasil) com:
- Webhook para confirmação de pagamento
- Reserva temporária (15-30 min) aguardando pagamento
- Estorno automático em cancelamentos
- Split de pagamento se houver múltiplos organizadores

---

### 1.2 Verificação de email é simulada — sem entrega real 🔴 9/10

**Arquivo:** [`src/Program.cs:588`](src/Program.cs:588)

```csharp
Console.WriteLine($"[EMAIL-VERIFICATION] Token para {dto.Email}: {token}");
```

A verificação de email **existe como fluxo** (token criptográfico de 32 bytes, expiração de 24h, endpoints de solicitação e confirmação), mas **não envia email algum**. O token é impresso no console do servidor.

A própria resposta da API admite:
```
"Token de verificação gerado. (Em produção, seria enviado por email.)"
```

**Isso não é verificação de email.** É um rascunho de implementação. Sem SMTP (SendGrid, Amazon SES, Mailgun, etc.), o usuário nunca recebe o token e a verificação é impossível.

**Impacto:** Contas fantasmas, impossibilidade de recuperação de senha, impossibilidade de notificar compradores.

**✅ Recomendação:** Implementar serviço de email real com provedor transacional:
- Envio de token de verificação no cadastro
- Envio de confirmação de compra com QR Code anexado
- Envio de instruções de cancelamento
- Notificações de alterações/cancelamentos de eventos

---

### 1.3 Métricas Prometheus retornam ZEROS hardcoded — monitoramento fake 🔴 8/10

**Arquivo:** [`src/Program.cs:690-707`](src/Program.cs:690-707)

A análise existente (#9.3) afirma: "Monitoramento implementado via endpoint GET /metrics com métricas no formato Prometheus."

**Isso é falso.** O endpoint retorna valores **estáticos e hardcoded**:

```csharp
metrics.Add("ticketprime_up 1");
metrics.Add("ticketprime_build_info{version=\"1.0.0\",environment=\"production\"} 1");
metrics.Add("ticketprime_database_up 1");
metrics.Add("ticketprime_request_duration_seconds_bucket{le=\"0.1\"} 0");  // sempre 0
metrics.Add("ticketprime_request_duration_seconds_bucket{le=\"0.5\"} 0");  // sempre 0
metrics.Add("ticketprime_requests_total{endpoint=\"all\"} 0");             // sempre 0
metrics.Add("ticketprime_uptime_seconds 0");                               // sempre 0
```

- `ticketprime_request_duration_seconds` — **sempre 0**, nenhum histograma real
- `ticketprime_requests_total` — **sempre 0**, nenhum contador real
- `ticketprime_uptime_seconds` — **sempre 0**, nenhum tracking de uptime real

**Isso não é monitoramento.** É um placeholder. Em produção, daria alertas falsos ou simplesmente não mostraria dados úteis.

**✅ Recomendação:** Implementar métricas reais com:
- Contadores incrementados em cada requisição (uso de middleware)
- Histograma de duração real (uso de `Stopwatch` em middleware)
- Rastreamento de erros por endpoint
- Métricas de negócio (ingressos vendidos, receita, cancelamentos)
- Ou usar biblioteca como `prometheus-net` em vez de reinventar a roda

---

### 1.4 Sem recuperação de senha funcional 🔴 7/10

**Arquivo:** [`src/Program.cs:306-327`](src/Program.cs:306-327)

O endpoint `trocar-senha` exige **senha atual** para trocar. Não há fluxo de "esqueci minha senha":
- Sem envio de link de redefinição por email
- Sem token de reset com expiração
- Sem perguntas de segurança

Se o usuário esquecer a senha, **não há como recuperar a conta**. O único recurso é contatar um administrador (que também não tem função para resetar senha de outros usuários).

**Impacto:** Perda permanente de contas de clientes com ingressos comprados.

---

### 1.5 Sem notificações ao comprador 🔴 7/10

Não há qualquer sistema de notificação:
- ✅ Compra realizada — sem email/SMS/notificação
- ✅ Ingresso cancelado — sem notificação
- ✅ Evento alterado/cancelado pelo organizador — sem aviso
- ✅ Lembrete de evento próximo — sem disparo

Para uma bilheteria profissional, notificações são **essenciais**.

---

## 2. 🔴 SEGURANÇA — Riscos residuais não endereçados

### 2.1 Rate limiting da política `escrita` não cobre todos os endpoints críticos 🔴 7/10

**Arquivo:** [`src/Program.cs:89-117`](src/Program.cs:89-117)

As políticas de rate limiting definidas são:
- `login`: 5/min — correta
- `escrita`: 10/min — boa
- `geral`: 100/min — cobrindo `/health`

Preciso verificar se `escrita` está aplicada em **todos** os endpoints de escrita. Vou verificar os endpoints no Program.cs.

---

### 2.2 Cookie httpOnly JWT sem `MaxAge` ou `Expires` para logout definitivo 🔴 6/10

**Arquivo:** [`src/Program.cs:281-288`](src/Program.cs:281-288)

Quando o JWT é definido como cookie httpOnly (`ticketprime_token`), não há mecanismo de **revogação** além da expiração natural. Se um usuário faz logout, o cookie não é limpo no servidor. O token continua válido até expirar.

**Impacto:** Logout incompleto — token pode ser reutilizado se interceptado antes da expiração.

---

### 2.3 Admin padrão sem 2FA 🔴 6/10

**Arquivo:** [`db/script.sql:230-233`](db/script.sql:230-233)

O admin padrão (CPF `00000000191`) não tem autenticação de dois fatores. Para uma plataforma que gerencia eventos, ingressos e finanças, um admin comprometido é **catastrófico**.

---

### 2.4 CSRF implementado mas frontend nunca envia `X-CSRF-TOKEN` 🔴 7/10

**Arquivo:** [`src/Program.cs:83-87`](src/Program.cs:83-87)

O backend configura Antiforgery com `HeaderName = "X-CSRF-TOKEN"` e middleware `UseAntiforgery()`, mas:

1. O frontend Blazor Server chama a API via `HttpClient` (veja [`ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs`](ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs))
2. **Nenhuma requisição HTTP inclui o header `X-CSRF-TOKEN`**
3. Logo, **todas as requisições POST/PUT/DELETE falham com 400** (CSRF inválido) ou o middleware não está configurado corretamente

O `AddAntiforgery` + `UseAntiforgery` no Minimal API precisa de configuração adicional para funcionar com chamadas AJAX. É necessário:
- Um endpoint `GET /api/antiforgery/token` que retorna o token CSRF
- O frontend deve ler esse token e incluí-lo em todas as requisições mutantes

**Status atual:** CSRF está **configurado incorretamente** — ou o middleware não está bloqueando (falsa sensação de segurança) ou está bloqueando requisições legítimas (sistema quebrado).

**✅ Recomendação:** Verificar se `UseAntiforgery()` está funcionando com chamadas AJAX. Se estiver bloqueando requisições sem `X-CSRF-TOKEN`, o sistema está quebrado. Se não estiver bloqueando, a proteção é ilusória.

---

## 3. 🟠 ARQUITETURA — Problemas estruturais não resolvidos

### 3.1 `CapacidadeTotal` é usado como "vagas disponíveis" — nome enganoso 🟠 7/10

**Arquivo:** [`src/Infrastructure/Repository/TransacaoCompraExecutor.cs:40`](src/Infrastructure/Repository/TransacaoCompraExecutor.cs:40)

```sql
UPDATE Eventos SET CapacidadeTotal = CapacidadeTotal - 1 WHERE Id = @EventoId AND CapacidadeTotal > 0
```

O campo `CapacidadeTotal` não armazena a **capacidade total** do evento. Ele armazena **quantas vagas ainda restam**. No cancelamento, ele é incrementado de volta.

**Isso é semanticamente incorreto.** Quando o admin cria um evento com `CapacidadeTotal = 100`, ele espera que esse valor permaneça 100 para sempre (a capacidade máxima do salão). Um campo separado `VagasDisponiveis` ou `VagasRestantes` deveria ser decrementado.

**Impacto:** Relatórios que mostrem `CapacidadeTotal` exibirão números enganosos após as primeiras vendas.

**✅ Recomendação:** Criar coluna `VagasRestantes` que inicia igual a `CapacidadeTotal` e é decrementada nas compras. Manter `CapacidadeTotal` como o valor original imutável.

---

### 3.2 Blazor Server + sessão in-memory = perda de estado ao desconectar 🟠 7/10

**Arquivos:** [`ui/TicketPrime.Web/Services/SessionService.cs`](ui/TicketPrime.Web/Services/SessionService.cs), [`ui/TicketPrime.Web/Program.cs:24`](ui/TicketPrime.Web/Program.cs:24)

O `SessionService` é registrado como **Scoped** (vida = circuito SignalR). Se a conexão SignalR cai (instabilidade de rede, reload, troca de aba em dispositivos móveis):

1. **Todo o estado da sessão é perdido** (CPF, Nome, Perfil, Token)
2. O usuário precisa fazer login novamente
3. Dados de formulários preenchidos são perdidos
4. Estado de páginas complexas (ex: EventoCreate.razor com fotos criptografadas) é zerado

Blazor Server é conhecido por essa fragilidade. Para uma bilheteria que precisa ser confiável, isso é um risco operacional.

**✅ Recomendação:** Considerar Blazor WebAssembly para partes críticas (compra de ingressos, perfil) ou implementar persistência de sessão via `ProtectedBrowserStorage`.

---

### 3.3 Inicialização do banco de dados frágil — path relativo quebra em Docker 🟠 6/10

**Arquivo:** [`src/Program.cs:715-775`](src/Program.cs:715-775)

```csharp
var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "db", "script.sql");
```

Em Docker:
- O diretório de trabalho (`WORKDIR`) é `/app`
- O script SQL está em `/app/db/script.sql` ou não existe no container

O método `InitializeDatabase` engole erros silenciosamente (`Console.WriteLine($"⚠ Warning during database setup: {ex.Message}")`). Se o script não for encontrado, a aplicação inicia **sem tabelas**, gerando erros misteriosos na primeira requisição.

**✅ Recomendação:** Script SQL deve ser embedado como recurso (Embedded Resource) no assembly, ou copiado para o diretório de trabalho no Dockerfile. Remover o path relativo `..`.

---

### 3.4 StartupDatabase no `Main` bloqueia inicialização 🟠 5/10

**Arquivo:** [`src/Program.cs:33`](src/Program.cs:33)

`InitializeDatabase(connectionString)` é chamado **sincronamente** no `Main`, antes do `builder.Build()`. Isso significa que:
- Se o SQL Server não estiver pronto, a aplicação **não inicia** (timeout de 60s)
- Em orquestração (Kubernetes/Docker Swarm), isso impede health checks e restart loops

O padrão correto seria usar `WebApplication.Services` no pipeline ou um hosted service.

---

## 4. 🟡 FRONTEND — Problemas de UX e qualidade

### 4.1 NavMenu — CSS duplicado persiste 🟡 5/10

**Arquivos:** [`NavMenu.razor`](ui/TicketPrime.Web/Components/Layout/NavMenu.razor), [`NavMenu.razor.css`](ui/TicketPrime.Web/Components/Layout/NavMenu.razor.css)

A análise existente (#4.6) afirma que o CSS duplicado foi removido e o `.razor.css` foi "limpo (apenas comentário)". No entanto, o arquivo [`NavMenu.razor`](ui/TicketPrime.Web/Components/Layout/NavMenu.razor) contém ~345 linhas com estilos inline em `<style>` tags. O componente mistura CSS de template antigo com customizações. Isso dificulta manutenção e aumenta o bundle.

---

### 4.2 Página Home.razor excessivamente longa (610 linhas) 🟡 4/10

**Arquivo:** [`ui/TicketPrime.Web/Components/Pages/Home.razor`](ui/TicketPrime.Web/Components/Pages/Home.razor)

610 linhas de componentes, CSS inline e HTML em um único arquivo. Todo o CSS está em `<style>` tags no próprio arquivo, sem separação em `Home.razor.css`. Para uma landing page, isso é aceitável, mas o padrão de CSS inline misturado com lógica de apresentação não escala.

---

### 4.3 Login não oferece "Lembrar-me" 🟡 3/10

**Arquivo:** [`ui/TicketPrime.Web/Components/Pages/Login.razor`](ui/TicketPrime.Web/Components/Pages/Login.razor)

Não há opção "Lembrar-me" (manter sessão por mais tempo). O JWT tem duração fixa configurável (30 min). Para usuários frequentes, isso significa múltiplos logins por dia.

---

### 4.4 Cadastro não oferece verificação de email imediata 🟡 4/10

**Arquivo:** [`ui/TicketPrime.Web/Components/Pages/CadastroUser.razor`](ui/TicketPrime.Web/Components/Pages/CadastroUser.razor)

O cadastro faz auto-login imediatamente após sucesso, sem exigir verificação de email. O fluxo de verificação de email existe nos endpoints, mas o frontend **nunca direciona o usuário** para verificar o email. Contas não verificadas podem usar o sistema normalmente.

---

### 4.5 `NotFound.razor` usa layout inconsistente 🟡 2/10

**Arquivo:** [`ui/TicketPrime.Web/Components/Pages/NotFound.razor`](ui/TicketPrime.Web/Components/Pages/NotFound.razor)

A página 404 foi melhorada conforme a análise existente (#4.4), mas usa `@layout MainLayout`. Páginas de erro deveriam usar um layout mais simples ou nenhum layout para evitar falhas de navegação.

---

### 4.6 Sem feedback tátil/sonoro para ações importantes 🟡 3/10

Ações críticas como:
- Compra de ingresso — sem confirmação sonora ou animação comemoração
- Cancelamento — sem confirmação dramática
- Cadastro — sem celebração visual

MudBlazor oferece Snackbar que é usada em alguns lugares, mas a experiência não é consistente.

---

## 5. 🔵 BACKEND — Problemas técnicos

### 5.1 CORS configurado de forma excessivamente permissiva 🔵 6/10

**Arquivo:** [`src/Program.cs:35-45`](src/Program.cs:35-45)

```csharp
policy.WithOrigins(origins)
      .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
      .AllowAnyHeader();
```

`AllowAnyHeader()` permite qualquer header, incluindo headers personalizados que poderiam ser usados para ataques. Para uma API que será consumida por múltiplos clientes, isso deveria ser mais restritivo.

---

### 5.2 Métricas endpoint sem controle de acesso 🔵 6/10

**Arquivo:** [`src/Program.cs:677-710`](src/Program.cs:677-710)

```csharp
app.MapGet("/metrics", ...).RequireRateLimiting("geral");
```

O endpoint `/metrics` expõe informações do sistema sem autenticação. Em produção, métricas Prometheus podem vazar:
- Informações de build (versão, ambiente)
- Estado do banco de dados (acessível/inacessível)
- Padrões de tráfego

Deveria ser restrito a IPs internos ou exigir autenticação.

---

### 5.3 Transação de compra sem verificação de estoque real 🔵 6/10

**Arquivo:** [`src/Infrastructure/Repository/TransacaoCompraExecutor.cs:40`](src/Infrastructure/Repository/TransacaoCompraExecutor.cs:40)

```sql
UPDATE Eventos SET CapacidadeTotal = CapacidadeTotal - 1 WHERE Id = @EventoId AND CapacidadeTotal > 0
```

Se `CapacidadeTotal` for 0, o UPDATE não afeta nenhuma linha, mas a transação não falha. O código assume que a linha foi atualizada sem verificar `@@ROWCOUNT`.

**Risco:** Em teoria, se a capacidade for 0 e o UPDATE não fizer nada, a reserva ainda é inserida, vendendo um ingresso que não existe.

---

### 5.4 Remoção de EF Core reduziu funcionalidade 🔵 5/10

A análise existente (#3.5) comemora a remoção dos pacotes EF Core como uma vitória ("aumenta o tamanho do container Docker, o tempo de build, e o surface de vulnerabilidades sem nenhum benefício").

Na verdade, EF Core ofereceria:
- **Migrations** — gerenciamento de schema versionado
- **Change tracking** — auditoria automática
- **Lazy loading** — navegação de objetos relacionais
- **Testes com banco em memória** — testes de integração simplificados

A remoção foi uma decisão técnica defensável (Dapper + scripts SQL), mas não é unambiguamente positiva. Perdeu-se capacidade de migration e testes de integração.

---

## 6. 🟣 DADOS E BANCO — Problemas de schema

### 6.1 Script SQL `ALTER` falha se tabelas não existirem 🟣 6/10

**Arquivo:** [`db/script.sql`](db/script.sql)

O script contém `ALTER TABLE` statements para adicionar colunas (`EmailVerificado`, `TokenVerificacaoEmail`, `TokenExpiracaoEmail`). Se executado em um banco novo, esses `ALTER` falham porque as colunas já foram criadas no `CREATE TABLE`.

O `InitializeDatabase` no [`Program.cs:754-762`](src/Program.cs:754-762) engole esses erros:
```csharp
catch (Exception ex)
{
    Console.WriteLine($"⚠ Database setup notice: {ex.Message}");
}
```

**Isso é frágil.** Erros de schema são engolidos silenciosamente. Uma falha real de migração passaria despercebida.

**✅ Recomendação:** Usar `IF NOT EXISTS` ou `IF COL_LENGTH('table', 'column') IS NULL` antes de `ALTER TABLE`. Melhor ainda, usar um sistema de migrations.

---

### 6.2 Coluna `Auditoria` criada mas nunca populada 🟣 5/10

**Arquivo:** [`db/script.sql:403-418`](db/script.sql:403-418)

A tabela `Auditoria` é criada no schema, mas:
- Nenhum INSERT é feito em nenhum service ou repository
- Nenhuma trigger registra mudanças
- Nenhum endpoint expõe dados de auditoria

É uma tabela **morta** — criada mas nunca usada.

---

### 6.3 Sem índices na tabela `Auditoria` 🟣 3/10

Se a tabela `Auditoria` fosse usada, cresceria rapidamente. Sem índices em `Entidade`, `EntidadeId` ou `DataHora`, consultas seriam full table scan.

---

## 7. ⚪ TESTES — O que está sendo testado de fato

### 7.1 Testes de segurança são extensos mas superficiais ⚪ 6/10

**Arquivo:** [`tests/SecurityTests.cs`](tests/SecurityTests.cs) (1003 linhas)

Pontos fortes: testa SQL injection (CPF, nome, email), CPF validation, password strength, JWT validation, rate limiting policies, CSRF, XSS sanitization.

**O que NÃO testa:**
- Ataque de força bruta em múltiplos endpoints (apenas testa as políticas, não o comportamento real)
- Injeção de headers maliciosos
- Path traversal em upload de fotos (não há upload de arquivo, são dados criptografados)
- Timing attack no login (comparação de hash)
- Authorization bypass (CLIENTE tentando acessar endpoint de ADMIN)
- Race condition na compra (testes unitários não simulam concorrência)

---

### 7.2 Testes de integração existem mas rodam contra banco real ⚪ 5/10

**Arquivos:** [`tests/Integration/DatabaseIntegrationTests.cs`](tests/Integration/DatabaseIntegrationTests.cs) (644 linhas), [`tests/Integration/IntegrationTestFixture.cs`](tests/Integration/IntegrationTestFixture.cs) (85 linhas)

Pontos fortes:
- Usam transação com rollback para isolar testes
- Testam CRUD completo de todas as entidades
- Testam fluxo completo de compra com cupom
- Testam concorrência básica (cancelamento por outro usuário falha)

**Fragilidades:**
- Dependem de SQL Server real — não rodam em CI sem banco configurado
- `IntegrationTestFixture.cs` usa `TEST_CONNECTION_STRING` com fallback para `Server=localhost\\SQLEXPRESS;...` — quebra em máquinas sem SQL Server
- Testes de integração podem ser frágeis e lentos
- Não testam o pipeline HTTP completo (end-to-end com autenticação JWT real)

---

### 7.3 Cobertura de código é ampla mas evita testar o fluxo HTTP real ⚪ 5/10

O que é testado:
- ✅ Todas as services camada (Auth, Evento, Reserva, Cupom, Usuario) — com mocks
- ✅ FluentValidation (validador de DTO)
- ✅ Models (Evento, EventoCreate)
- ✅ DB integration (CRUD + fluxos completos)
- ✅ Segurança (JWT, SQLi, XSS, rate limiting)

**O que NÃO é testado:**
- ❌ **Nenhum teste de API/integration** — os endpoints Minimal API em [`Program.cs`](src/Program.cs) não são testados como HTTP
- ❌ **Nenhum teste de frontend** — componentes Blazor não têm testes
- ❌ **Nenhum teste E2E** — Playwright/Cypress/Selenium
- ❌ **Nenhum teste de carga** — não se sabe se o sistema aguenta 1000 compras simultâneas

---

## 8. 🟤 DEVOPS — Problemas de deployment

### 8.1 Docker Compose sem health checks para API e Frontend 🟤 6/10

**Arquivo:** [`docker-compose.yml`](docker-compose.yml)

O serviço `sqlserver` tem health check, mas `api` e `frontend` não. Docker Compose não reiniciará esses serviços se falharem.

---

### 8.2 Sem variáveis de ambiente para configurações sensíveis no Docker 🟤 6/10

O `Jwt:Key` é configurável via `${JWT_KEY}`, mas outras configurações como `BCryptWorkFactor`, `ExpireMinutes`, `AllowedOrigins` não são expostas como variáveis de ambiente no Docker Compose. Para produção, toda configuração sensível deveria vir de env vars.

---

### 8.3 Sem configuração de CI/CD 🟤 5/10

Não há arquivos de CI/CD (GitHub Actions, GitLab CI, etc.). O projeto não tem:
- Build automatizado
- Testes rodando em CI
- Publicação de imagens Docker
- Deploy automatizado

---

### 8.4 Dockerfile sem multi-stage build otimizado 🟤 5/10

**Arquivo:** [`src/Dockerfile`](src/Dockerfile)

O Dockerfile provavelmente compila no container final sem separar build e runtime. Projetos .NET modernos usam multi-stage build com `mcr.microsoft.com/dotnet/sdk` para build e `mcr.microsoft.com/dotnet/aspnet` para runtime, reduzindo o tamanho da imagem final.

---

### 8.5 Nginx com rate limiting de 200r/s pode ser insuficiente 🟤 4/10

**Arquivo:** [`docker/nginx/nginx.conf`](docker/nginx/nginx.conf)

```nginx
limit_req_zone $binary_remote_addr zone=mylimit:10m rate=200r/s;
```

200 requisições por segundo por IP é razoável, mas 10MB de zona pode ser insuficiente para muitos usuários simultâneos (~160.000 endereços únicos antes de começar a rejeitar).

---

## 9. ⚫ DOCUMENTAÇÃO E OBSERVABILIDADE

### 9.1 Documentação abundante mas desatualizada ⚫ 5/10

Existem 12+ arquivos de documentação em [`docs/`](docs/), muitos dos quais referenciam arquivos ou comportamentos que não existem mais (ex: `CriarEvento.razor` em vez de `EventoCreate.razor`). A análise existente (#9.2) diz que foi corrigida em 8 arquivos, mas documentos como `ANALISE_CLIENTE_TICKETPRIME.md` (358 linhas) ainda contêm informações desatualizadas.

---

### 9.2 Sem documentação de API (Swagger/OpenAPI) ⚫ 6/10

**Arquivo:** [`src/src.csproj`](src/src.csproj)

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.3.1" />
```

O pacote Swashbuckle está incluído (linha 15), mas não há confirmação de que o Swagger está configurado no pipeline. Para uma API que será consumida por frontend (ou terceiros no futuro), documentação OpenAPI é essencial.

Verificando no Program.cs: não há `app.UseSwagger()` ou `app.UseSwaggerUI()`. O pacote Swashbuckle está instalado mas **não está configurado** — mais um dead dependency.

---

### 9.3 Sem logging de auditoria funcional ⚫ 6/10

Embora a tabela `Auditoria` exista no banco (veja #6.2), não há logging de:
- Quem criou/deletou/alterou eventos
- Quem emitiu/reembolsou ingressos
- Quem criou/alterou cupons
- Tentativas de login falhas (por IP, por CPF)

---

## 10. 📊 REAVALIAÇÃO DA NOTA E VEREDITO

### Minha avaliação independente vs. Análise existente

| Categoria | Análise Existente | Minha Avaliação | Diferença |
|-----------|:---:|:---:|:---:|
| 🔴 Bloqueantes (bugs) | ✅ 4/4 (100%) | ✅ 4/4 (100%) | — |
| 🔴 Segurança | ✅ 10/10 (100%) | ⚠️ Há CSRF mal configurado, sem 2FA, sem revogação | Itens críticos residuais |
| 🟠 Arquitetura | ✅ 5/5 (100%) | ⚠️ `CapacidadeTotal` naming, Blazor Server fragilidade, DB init frágil | 3 gaps não endereçados |
| 🟡 Frontend | ✅ 7/7 (100%) | ⚠️ CSS duplicado, Home.razor inchado, sem verificação de email | Melhorias não concluídas |
| 🔵 Backend | ✅ 7/7 (100%) | ⚠️ CORS permissivo, metrics sem auth, transação frágil | Riscos residuais |
| 🟣 Dados/Banco | ✅ 3/3 (100%) | ⚠️ Script SQL frágil, tabela Auditoria morta | Problemas reais |
| ⚪ Testes | ⚠️ 2/4 (50%) | ⚠️ Sem testes de API/HTTP, sem E2E, sem carga | Subestimado |
| 🟤 DevOps | ✅ 4/4 (100%) | ⚠️ Sem CI/CD, sem health checks em serviços | Gaps operacionais |
| ⚫ Outros | ✅ 3/3 (100%) | 🔴 Swagger não configurado, sem auditoria real | Falso positivo |
| **🔴 GAPS FUNCIONAIS** | **NÃO AVALIADO** | **🔴 5 itens críticos** | **MAIOR GAP** |

### 🔴 Os 5 Gaps Funcionais que a Análise Existente Ignora

| # | Gap | Criticidade | Impacto |
|---|-----|:-----------:|---------|
| 1 | **Sem processamento de pagamento** — bilheteria não cobra | 🔴 10/10 | Sistema inutilizável para venda real |
| 2 | **Email verification é Console.WriteLine** — sem entrega real | 🔴 9/10 | Contas fantasmas, sem recuperação |
| 3 | **Métricas Prometheus são zeros hardcoded** — monitoramento fake | 🔴 8/10 | Sem observabilidade real |
| 4 | **CSRF configurado incorretamente** — ou bloqueia tudo ou não protege | 🔴 7/10 | Sistema quebrado ou sem proteção |
| 5 | **Sem recuperação de senha** — conta perdida = irreversível | 🔴 7/10 | Suporte sobrecarregado, clientes frustrados |

### Nota Final

| Aspecto | Nota |
|---------|:----:|
| **Segurança** (BCrypt, JWT httpOnly, rate limiting, XSS sanitization, CPF validation) | 8.5/10 |
| **Arquitetura** (camadas limpas, Dapper + SQL, DI, testes) | 7.5/10 |
| **Frontend** (MudBlazor, tema escuro, E2E encryption, páginas responsivas) | 7.0/10 |
| **Testes** (8 arquivos, ~2600+ linhas, unit + integration + security) | 7.0/10 |
| **DevOps** (Docker Compose, nginx, TLS, scripts) | 6.5/10 |
| **Funcionalidades de negócio** (pagamento, email, notificações, recuperação) | **2.0/10** |

> **Nota geral (ponderada): 6.0/10**
>
> A nota da análise existente (9.2/10) reflete apenas a qualidade do **código escrito**, ignorando completamente **o que falta escrever**. Um sistema de bilheteria sem pagamento é como um carro sem rodas — tecnicamente bem construído, mas funcionalmente incapaz de cumprir seu propósito.

---

### 🎯 VEREDITO FINAL DO COMPRADOR

**Pontos fortes reais:**
- ✅ Segurança sólida (BCrypt work factor 11, JWT httpOnly + cookie, rate limiting em 3 níveis, sanitização XSS, validação de CPF com dígitos verificadores)
- ✅ Criptografia E2E real (ECDH P-256 + AES-GCM-256 + AES-KW no navegador Web Crypto API)
- ✅ Arquitetura em camadas limpa (Models → DTOs → Services → Repositories)
- ✅ Dapper com parâmetros (SQL injection prevenido)
- ✅ Transações com UPDLOCK (race condition prevenida em compras)
- ✅ Refresh token com rotação e armazenamento hasheado (SHA256)
- ✅ Testes abrangentes (8 arquivos, ~2600+ linhas)
- ✅ Docker Compose com nginx, TLS e 4 serviços
- ✅ Documentação abundante

**⚠️ O que preocupa como comprador:**
1. **O core do negócio não existe** — sem pagamento, sem Pix, sem cartão. O sistema "vende" ingressos sem cobrar.
2. **Funcionalidades parecem completas mas são placeholders** — email verification imprime no console, Prometheus metrics retorna zeros, tabela de auditoria nunca é populada, Swagger está instalado mas não configurado.
3. **CSRF está configurado incorretamente** — ou quebra o sistema ou não protege. Isso precisa ser verificado urgentemente.
4. **CapacidadeTotal com nome enganoso** — um relatório de "capacidade total do evento" exibirá números errados após as primeiras vendas.
5. **Blazor Server sem WASM** — experiência frágil em redes instáveis, comum em eventos com grande concentração de usuários.

**💰 Minha posição como comprador:**
- O projeto tem **uma base técnica excelente** — segurança, arquitetura, testes e infraestrutura estão bem encaminhados
- Mas está **funcionalmente incompleto** para o propósito declarado
- Estimativa de esforço para completar: **~3-6 meses** (integrando pagamento Pix, email transacional, notificações, recuperação de senha, correção do CSRF, refatoração do CapacidadeTotal)
- **Não compraria o projeto no estado atual para uso em produção.** Compraria apenas como uma base técnica para continuar o desenvolvimento, com desconto significativo refletindo o trabalho restante.

---

*Análise independente realizada em maio de 2026. Baseada na leitura completa de todos os ~50+ arquivos fonte do projeto (~13.000+ linhas). Documento confidencial para due diligence de compra.*
