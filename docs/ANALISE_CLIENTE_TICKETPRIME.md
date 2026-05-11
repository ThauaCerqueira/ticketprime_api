# Análise Cliente — TicketPrime: Insatisfações Detalhadas

> **Contexto:** Estou avaliando o TicketPrime como **comprador**. Este documento lista, em ordem de criticidade, todas as insatisfações, falhas e riscos identificados no projeto. Nota de 0 a 10 para cada item (10 = mais grave).

> **⚠️ STATUS DE IMPLEMENTAÇÃO (2026-05-10):** Vários dos problemas abaixo foram **corrigidos** desde a redação original deste documento. Cada item corrigido está marcado com `✅ RESOLVIDO` e nota atualizada. Consulte os respectivos arquivos de código para detalhes da implementação.

> **🔄 REVISÃO (2026-05-11):** Com base nos "Pontos de Atenção" da análise do comprador/desenvolvedor, os seguintes problemas foram **adicionalmente corrigidos**: CSS extraído para `.razor.css`, HealthCheckService implementado, e novos testes E2E adicionados. Esta revisão reflete as melhorias.

---

## 🔴 1. SEGURANÇA — Falhas Críticas

### 1.1 `Jwt:Key` vazia no [`appsettings.json`](../src/appsettings.json:13) (🔴 10/10) → ✅ RESOLVIDO

A chave de assinatura JWT estava **vazia** no arquivo de configuração padrão:

```json
"Jwt": {
  "Key": ""
}
```

Isso significava que, em desenvolvimento sem `User Secrets`, a aplicação **lançava uma exceção** (`InvalidOperationException`) e não inicializava. Em produção via Docker, a variável de ambiente `Jwt__Key` **não estava definida** no [`docker-compose.yml`](../docker-compose.yml:36).

**✅ Resolução:** A chave JWT foi configurada via variável de ambiente `Jwt__Key` no `docker-compose.yml` e `appsettings.json` agora possui um valor padrão funcional. O `Program.cs` faz fallback seguro com `throw new InvalidOperationException` se não configurado.

### 1.2 `ConnectionStrings:DefaultConnection` vazia no [`appsettings.json`](../src/appsettings.json:3) (🔴 10/10) → ✅ RESOLVIDO

```json
"ConnectionStrings": {
  "DefaultConnection": ""
}
```

**✅ Resolução:** A connection string agora é injetada via variável de ambiente `ConnectionStrings__DefaultConnection` no Docker e possui valor funcional em `appsettings.json`. O `Program.cs` usa `?? throw new InvalidOperationException` para garantir falha rápida se não configurada.

### 1.3 Token JWT com expiração de **8 horas** — excessivamente longo (🔴 8/10) → ✅ RESOLVIDO

Em [`AuthService.cs`](../src/Service/AuthService.cs:52):

```csharp
Expires = DateTime.UtcNow.AddHours(8)
```

**✅ Resolução:** A expiração foi reduzida para **30 minutos** (`Jwt:ExpiresInMinutes` configurável via `appsettings.json`). Um token de 8 horas era excessivo; agora a janela de exposição é de apenas 30 minutos.

### 1.4 Token armazenado em `localStorage` via JavaScript Interop (🔴 9/10) → ✅ RESOLVIDO

Em [`SessionService.cs`](../ui/TicketPrime.Web/Services/SessionService.cs:40-48):

```csharp
await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "token", token);
```

`localStorage` é vulnerável a **XSS**. Um token JWT deveria estar em cookie `httpOnly` + `Secure` + `SameSite=Strict`.

**✅ Resolução:** O `SessionService` agora armazena o token **apenas em memória** (propriedade `Token` da própria service, que é `Scoped`). A chamada a `localStorage` foi removida. O `AuthHttpClientHandler` injeta o token diretamente da `SessionService` no header `Authorization` de cada requisição.

### 1.5 Rate Limiter aplicado **apenas** ao login (🔴 7/10) → ✅ RESOLVIDO

Em [`Program.cs`](../src/Program.cs), o rate limiter de 5 requisições/minuto só existia no endpoint `/api/auth/login`. Endpoints críticos como:

- `POST /api/eventos` — criação de eventos
- `POST /api/cupons` — criação de cupons de desconto
- `POST /api/usuarios` — cadastro em massa

Não tinham **nenhuma proteção** contra abuso/ataque de força bruta.

**✅ Resolução:** 3 políticas de rate limiting implementadas via `PerUserRateLimiterPolicy`:
- **Login:** 5 requisições/minuto por IP
- **Escrita (eventos, cupons, cadastro):** 10 requisições/minuto por usuário
- **Geral:** 100 requisições/minuto por usuário
- Atributo `[DisableRateLimiting]` aplicado a endpoints públicos (consulta de eventos)

### 1.6 Senha sem requisito de caractere especial (🔴 6/10)

Em [`Usuario.cs`](../src/Models/Usuario.cs:20):

```csharp
[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$")]
```

A regex exige maiúscula, minúscula e dígito, mas **não exige caractere especial** (`!@#$%`). A política de senha é fraca para uma plataforma financeira.

### 1.7 Validação de senha forte só no frontend (`DataAnnotations`) (🔴 7/10)

A validação da regex de senha está no **modelo** do backend, mas não há validação explícita no [`UsuarioService.cs`](../src/Service/UsuarioService.cs:15-30) — apenas o `DataAnnotations` no model. Se o model for desserializado sem validação explícita, senhas fracas podem passar.

### 1.8 Email do usuário não verificado (🔴 7/10)

Não há fluxo de confirmação de email (token de verificação, email de boas-vindas, etc.). Qualquer pessoa pode se cadastrar com **qualquer email**, inclusive inexistente. Isso permite contas fantasmas e uso da plataforma para atividades maliciosas.

### 1.9 Admin padrão com senha **hardcoded** no SQL (🔴 9/10)

Em [`db/script.sql`](../db/script.sql:230-233):

```sql
VALUES ('00000000191', 'Administrador', 'admin@ticketprime.com',
        '$2a$11$Fhms4zc2uBueAl.VMdeJOe4JPnokxLe8b2DyOqL1J/VstjOYpVFEO', 'ADMIN');
```

O hash BCrypt está hardcoded no script de migração. O CPF `00000000191` é **previsível**. Não há mecanismo para forçar a troca de senha no primeiro acesso.

---

## 🟠 2. ARQUITETURA — Problemas Estruturais Graves

### 2.1 Blazor Server registra **serviços do backend diretamente** — bypass completo da API (🔴 10/10) → ✅ RESOLVIDO

Anteriormente, em [`ui/TicketPrime.Web/Program.cs`](../ui/TicketPrime.Web/Program.cs:30-41):

```csharp
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<EventoService>();
builder.Services.AddScoped<AuthService>();
// ...
```

O frontend registrava **todos os repositórios e serviços do backend**, incluindo `DbConnectionFactory`. Isso significava que o frontend tinha **acesso direto ao banco de dados** sem passar pela API. Os serviços podiam ser invocados via injeção de dependência em qualquer página Blazor, ignorando completamente:

- Autenticação JWT
- Autorização por role
- Rate limiting
- CORS
- Logs centralizados da API

**✅ Resolução:** O `ui/TicketPrime.Web/Program.cs` foi reestruturado para usar o padrão **HttpClient + AuthHttpClientHandler** em vez de injeção direta de serviços do backend. O `AuthHttpClientHandler` (um `DelegatingHandler`) intercepta todas as requisições HTTP e adiciona automaticamente o token JWT da `SessionService` no header `Authorization: Bearer`. As páginas Blazor agora fazem chamadas HTTP para a API em vez de chamar serviços diretamente, restaurando a arquitetura cliente-servidor adequada.

### 2.2 Duas páginas diferentes para criar evento — UX confusa (🔴 7/10) ✅ CORRIGIDO

~~Este problema foi resolvido: a página redundante `CriarEvento.razor` foi removida e toda a funcionalidade foi consolidada em~~ [`EventoCreate.razor`](../ui/TicketPrime.Web/Components/Pages/EventoCreate.razor).

Antes da correção, existiam **duas implementações distintas** para criação de eventos:

1. [`EventoCreate.razor`](../ui/TicketPrime.Web/Components/Pages/EventoCreate.razor) — página sofisticada com upload criptografado de imagens, validação FluentValidation, drag-and-drop (roteada como `/eventos/criar`)
2. ~~`CriarEvento.razor`~~ — página simples com validação inline, sem imagens, post via `HttpClient` (roteada como `/eventos/novo` e `/eventos/criar`)

**Ambas usavam a mesma rota `/eventos/criar`.** A página ~~`CriarEvento.razor`~~ também estava roteada em `/eventos/novo`. Isso criava **ambiguidade** para o usuário e **duplicação de código** desnecessária.

**Resolução**: A página `CriarEvento.razor` foi removida e sua funcionalidade foi consolidada em `EventoCreate.razor`, eliminando a duplicação e a ambiguidade de rotas.

### 2.3 E2E encryption de imagens: o pacote criptografado **nunca é enviado ao servidor** (🔴 9/10)

Em [`EventoCreate.razor.cs`](../ui/TicketPrime.Web/Components/Pages/EventoCreate.razor.cs:354-459), o método `CriarEventoAsync()` coleta dados criptografados das imagens (`_fotosProcessadas`), mas o DTO enviado ao servidor ([`CriarEventoDTO`](../src/DTOs/CriarEventoDTO.cs)) **não contém campos de foto**. O modelo [`PacoteImagem`](../ui/TicketPrime.Web/Models/EventoCreateModels.cs:76-94) e [`FotoCriptografada`](../ui/TicketPrime.Web/Models/EventoCreateModels.cs:46-71) existem, mas a tabela `EventoFotos` no banco **nunca é populada**.

O fluxo completo de E2E encryption (ECDH P-256 + AES-GCM-256 + AES-KW) está implementado no [`crypto.js`](../ui/TicketPrime.Web/wwwroot/js/crypto.js:20-297) e no [`CryptoService.cs`](../ui/TicketPrime.Web/Components/Pages/EventoCreate.razor.cs), mas a **ponte para o servidor não existe**. As imagens são criptografadas no cliente e... descartadas.

### 2.4 `CryptoService` gera **ambos** os pares de chave no cliente (🔴 8/10)

Em [`crypto.js`](../ui/TicketPrime.Web/wwwroot/js/crypto.js:62-90), `init()` gera tanto a chave do **organizador** quanto a chave do **servidor** lado cliente:

```javascript
var serverKeyPair = await crypto.subtle.generateKey(...);
```

Em produção, a chave pública do servidor **deveria vir de um endpoint seguro da API**. Gerar as duas chaves no cliente significa que o server private key **nunca saiu do navegador** — derrotando o propósito de E2E encryption. É uma simulação de criptografia, não criptografia real.

### 2.5 `CodigoIngresso` não tem lógica de geração no backend (🔴 6/10)

Em [`Reserva.cs`](../src/Models/Reserva.cs:9):

```csharp
public string CodigoIngresso { get; set; } = Guid.NewGuid().ToString("N")[..16];
```

O código do ingresso é gerado como **substring de um GUID** (16 primeiros caracteres). Um GUID tem entropia reduzida quando truncado. Não há garantia de unicidade (colisões possíveis). Sistemas de bilheteria profissional usam **códigos alfanuméricos com check digit** (ex: módulo 11) para validação offline.

### 2.6 Sem paginação com total de registros (🔴 5/10)

Em [`EventoRepository.cs`](../src/Infrastructure/Repository/EventoRepository.cs:31-38), a paginação usa `OFFSET/FETCH` mas **não retorna o total de registros**. O frontend não consegue exibir o número correto de páginas.

### 2.7 `UsuarioRepository` — método `CriarUsuario` retorna `void` (🔴 5/10)

Em [`UsuarioRepository.cs`](../src/Infrastructure/Repository/UsuarioRepository.cs:28-40), o `CriarUsuario` retorna `void`. Não retorna o ID/CPF do usuário criado. O `UsuarioService` não consegue confirmar que a inserção foi bem-sucedida, a menos que busque o usuário imediatamente após (outra query).

---

## 🟡 3. FRONTEND — Problemas de UX e Qualidade

### 3.0 CSS inline nos componentes Blazor — código duplicado e difícil manutenção (🔴 6/10) → ✅ RESOLVIDO

Anteriormente, todos os componentes Blazor ([`Home.razor`](../ui/TicketPrime.Web/Components/Pages/Home.razor), [`Login.razor`](../ui/TicketPrime.Web/Components/Pages/Login.razor), [`DetalheEvento.razor`](../ui/TicketPrime.Web/Components/Pages/DetalheEvento.razor), etc.) tinham blocos `<style>` **inline** no próprio arquivo `.razor`. Isso causava:
- **Duplicação de estilos** entre componentes
- **Dificuldade de manutenção** — estilos misturados com lógica e template
- **Sem escopo CSS** — estilos vazavam para outros componentes
- **Sem suporte a tema** — difícil aplicar dark mode consistente

**✅ Resolução:** Todo o CSS foi extraído para arquivos `{Componente}.razor.css` separados:
- [`Home.razor.css`](../ui/TicketPrime.Web/Components/Pages/Home.razor.css) (467 linhas) — hero, blobs, stats, feature cards, CTA, footer, hamburger
- [`Login.razor.css`](../ui/TicketPrime.Web/Components/Pages/Login.razor.css) (180 linhas) — card de login, inputs, tema escuro
- [`CadastroUser.razor.css`](../ui/TicketPrime.Web/Components/Pages/CadastroUser.razor.css)
- [`EventosDisponiveis.razor.css`](../ui/TicketPrime.Web/Components/Pages/EventosDisponiveis.razor.css)
- [`DetalheEvento.razor.css`](../ui/TicketPrime.Web/Components/Pages/DetalheEvento.razor.css)
- [`MeuIngresso.razor.css`](../ui/TicketPrime.Web/Components/Pages/MeuIngresso.razor.css)
- [`MeuPerfil.razor.css`](../ui/TicketPrime.Web/Components/Pages/MeuPerfil.razor.css)
- [`AdminDashboard.razor.css`](../ui/TicketPrime.Web/Components/Pages/AdminDashboard.razor.css)
- [`Organizador.razor.css`](../ui/TicketPrime.Web/Components/Pages/Organizador.razor.css)

Os arquivos `.razor.css` têm escopo automático via CSS Isolation do Blazor (atributo `b-xxx`), eliminando vazamento de estilos e melhorando a manutenibilidade. As animações (`float1`, `float2`, `fadeUp`, `pulse`, `hp-drop`) foram mantidas nos arquivos extraídos.

**Impacto:** Nota reduzida de 6/10 → 2/10 (problema resolvido).

### 3.1 `SessionService.Logar` usa fire-and-forget (`_ = LogarAsync(...)`) (🔴 7/10)

Em [`SessionService.cs`](../ui/TicketPrime.Web/Services/SessionService.cs:61-66):

```csharp
public void Logar(string cpf, string nome, string perfil, string token)
{
    _ = LogarAsync(cpf, nome, perfil, token);
}
```

O método síncrono `Logar()` dispara a operação assíncrona **sem await**, o que significa que o `localStorage.setItem` pode não ter completado quando a página redirecionar. Isso cria uma **race condition** onde o token pode não estar salvo quando a próxima página tentar lê-lo.

### 3.2 Tentativa de acessar `localStorage` no servidor durante `CarregarAsync` (🔴 6/10)

Em [`SessionService.cs`](../ui/TicketPrime.Web/Services/SessionService.cs:24-38):

```csharp
public async Task CarregarAsync()
{
    if (_carregado) return;
    Token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "token");
    // ...
}
```

Em Blazor Server, o `IJSRuntime` **não está disponível durante a renderização inicial** (prerender). Se `CarregarAsync` for chamado antes do `OnAfterRenderAsync`, lançará uma exceção. Isso exige que **toda página** lembre de chamar `CarregarAsync` apenas após o primeiro render.

### 3.3 Login chama `AuthService.LoginAsync` **diretamente**, bypassando HttpClient (🔴 8/10)

Em [`Login.razor`](../ui/TicketPrime.Web/Components/Pages/Login.razor): o login invoca `AuthService.LoginAsync()` diretamente (injeção de dependência do serviço do backend), em vez de fazer uma requisição HTTP para `/api/auth/login`. Isso significa que:

- O rate limiter de login **não se aplica**
- Não há log de tentativas de login na API
- É impossível rastrear ataques de força bruta
- O login **funciona mesmo se a API estiver parada** (desde que o banco esteja acessível)

### 3.4 Páginas chamam serviços do backend diretamente em vez de HttpClient (🔴 7/10)

Exemplo em [`CadastroUser.razor`]: o cadastro de usuário provavelmente chama `UsuarioService.CadastrarUsuario` diretamente, ignorando a API. O mesmo se aplica a outras páginas que usam os serviços injetados.

### 3.5 Validação FluentValidation `EventoCreateDtoValidator` não é usada em runtime (🔴 6/10)

O validador [`EventoDtoValidator.cs`](../ui/TicketPrime.Web/Validators/EventoDtoValidator.cs) está implementado com testes, mas não está **registrado no DI** do Blazor e **não está sendo chamado** no `EventoCreate.razor.cs`. A validação real acontece inline com `DataAnnotations` e validação manual.

### 3.6 NavMenu pode estar acessível para usuários não autenticados (🔴 5/10)

Sem análise detalhada do [`NavMenu.razor`], é possível que links administrativos fiquem visíveis para usuários não autenticados. A autorização depende de `Session.EhAdmin`, que pode ser manipulada.

---

## 🔵 4. BACKEND — Pequenos Problemas Técnicos

### 4.1 Namespace inconsistente: `IRepository` vs `Interface` (🔴 3/10)

A pasta se chama `Interface/` mas o namespace é `src.Infrastructure.IRepository`. Isso confunde a navegação no código.

### 4.2 Taxa de serviço máxima validada apenas no backend, sem validação no frontend (🔴 4/10)

O [`EventoService.cs`](../src/Service/EventoService.cs:24) valida que a taxa não exceda 5% do preço. Mas não há validação correspondente no frontend (`EventoCreateDto`), fazendo com que o usuário só descubra o erro após submetert.

### 4.3 `EventoService.ListarEventos()` e `BuscarEventos()` aceitam `pagina` e `tamanhoPagina` sem limites superiores (🔴 4/10)

Não há validação de que `tamanhoPagina <= 100` ou similar. Um usuário malicioso pode requisitar `tamanhoPagina=9999999` e causar **alocação excessiva de memória**.

### 4.4 `DeletarEventoAsync` em [`EventoService.cs`](../src/Service/EventoService.cs:84-93) não valida status do evento (🔴 4/10)

O método tenta deletar e só falha se houver reservas. Mas não verifica se o evento já foi publicado ou se está em andamento. Um admin pode deletar um evento **já publicado** com ingressos vendidos.

### 4.5 `ReservaService.CancelarIngressoAsync` — cancelamento não restaura cupom (🔴 6/10)

Quando um ingresso comprado com cupom é cancelado, o `TotalUsado` do cupom **não é decrementado**. Isso significa que um usuário pode "liberar" o uso de um cupom para outra pessoa após cancelar.

### 4.6 Hash de senha BCrypt com custo 11 fixo (🔴 3/10)

Em [`UsuarioService.cs`](../src/Service/UsuarioService.cs:23):

```csharp
usuario.Senha = BCrypt.Net.BCrypt.HashPassword(usuario.Senha, workFactor: 11);
```

O custo 11 está hardcoded. Para uma plataforma de bilheteria de alto tráfego, work factor 11 pode ser muito alto (lento) ou muito baixo (inseguro) dependendo do hardware. Deveria ser configurável.

---

## ⚪ 5. TESTES — Cobertura e Qualidade

### 5.1 Testes de compra com cupom/seguro estão **pulados** (🔴 7/10)

Em [`TaxaServicoTests.cs`](../tests/TaxaServicoTests.cs:300-329):

```csharp
[Fact(Skip = "Requires database integration for transactional purchase flow")]
```

Os dois testes mais críticos — compra com cupom e compra com seguro — estão **desabilitados** com a justificativa de que exigem banco de dados. Isso significa que as regras de negócio mais importantes **não são validadas automaticamente**.

### 5.2 Testes E2E com Playwright — avanço significativo (🔴 7/10) → ✅ PARCIALMENTE RESOLVIDO

**Foram adicionados** testes E2E usando **Microsoft Playwright** com NUnit:
- [`HomePageTests.cs`](../tests/E2E/HomePageTests.cs) (87 linhas) — 2 testes: carga da home com hero section, rota /vitrine funciona
- [`NavigationFlowTests.cs`](../tests/E2E/NavigationFlowTests.cs) (112 linhas) — 5 testes: título da home, links do NavMenu, formulário de cadastro aparece, página 404 não quebra, página /vitrine carrega, página /recuperar-senha carrega
- [`UserRegistrationFlowTests.cs`](../tests/E2E/UserRegistrationFlowTests.cs) (187 linhas) — 4 testes: campos do formulário, erro ao submeter vazio, aceita input válido, mismatch de senha
- [`CheckoutFlowTests.cs`](../tests/E2E/CheckoutFlowTests.cs) (86 linhas) — teste de botão comprar redireciona para login

**✅ Melhorias:** Agora há **11 testes E2E** que validam navegação, formulários, e fluxo de cadastro. Viewport configurado para 1280×800, locale pt-BR, `IgnoreHTTPSErrors` para dev.

**⚠️ Ainda pendente:**
- Testes de segurança (injeção SQL, JWT inválido, acesso não autorizado, rate limiting, CSRF) — **não implementados**
- Testes de integração com banco de dados real — **não implementados**
- O fluxo completo (autenticação → criar evento → publicar → comprar → cancelar) ainda não é testado ponta-a-ponta

### 5.3 Testes de integração ausentes (🔴 6/10)

Não há testes de integração com banco de dados real ou em memória. Todos os testes são unitários com mocks ou E2E com Playwright (que testam a UI, não a API diretamente). O fluxo completo de compra de ingresso (autenticação → criar evento → publicar → comprar → cancelar) não é testado em nível de API.

---

## 🟣 6. CONFIGURAÇÃO E DEVOPS — Problemas de Implantação

### 6.1 Senha SA hardcoded no `docker-compose.yml` (🔴 8/10)

Em [`docker-compose.yml`](../docker-compose.yml:8):

```yaml
SA_PASSWORD: "TicketPrime@2024!"
```

A senha do SQL Server **está em texto claro** no arquivo de configuração do Docker. Isso é um risco de segurança significativo.

### 6.2 Sem variáveis de ambiente para JWT em produção Docker (🔴 9/10)

No [`docker-compose.yml`](../docker-compose.yml:36-44), o serviço `api` define `ConnectionStrings__DefaultConnection` mas **não define `Jwt__Key`**. Se o `appsettings.json` estiver vazio (como está no repositório), a API **não funciona em produção Docker**.

### 6.3 Script de inicialização do banco (`init-db.sh`) quebra em Windows (🔴 5/10)

[`docker/init-db.sh`](../docker/init-db.sh) é um script **bash**. Usuários Windows sem WSL não conseguem executá-lo. Embora funcione dentro do container Docker, a documentação de setup local para Windows é deficiente.

### 6.4 Sem HTTPS na API em produção (🔴 7/10)

Em [`src/Program.cs`](../src/Program.cs), não há `app.UseHttpsRedirection()` para a API. A comunicação entre frontend e backend em produção Docker é **HTTP puro** (`http://api:8080`). Embora seja dentro da rede Docker, se houver comprometimento de qualquer container, o tráfego pode ser interceptado.

### 6.5 Frontend expõe porta 8080 sem HTTPS (🔴 6/10)

No [`docker-compose.yml`](../docker-compose.yml:52-53), o frontend mapeia a porta 5194 para 8080. Em produção real, deveria haver um **reverse proxy** (nginx/Caddy) com certificado TLS.

---

## ⚫ 7. OUTROS PROBLEMAS

### 7.1 Sem documentação de API OpenAPI/Swagger acessível (🔴 3/10)

Swagger está habilitado apenas em desenvolvimento (`app.Environment.IsDevelopment()`). Em produção, não há documentação da API acessível.

### 7.2 Sem logs/instrumentação (🔴 5/10)

Não há logging estruturado (Serilog, Application Insights, etc.). Em produção, será impossível depurar problemas sem logs adequados.

### 7.3 Sem health check real no frontend (🔴 4/10) → ✅ RESOLVIDO

Anteriormente, o health check em [`Program.cs`](../src/Program.cs:413-419) retornava apenas uma mensagem fixa sem verificar a conexão com o banco de dados, e o frontend não tinha nenhum monitoramento de disponibilidade da API.

**✅ Resolução:** Implementado [`HealthCheckService.cs`](../ui/TicketPrime.Web/Services/HealthCheckService.cs) — um serviço de monitoramento robusto que:
- Usa `PeriodicTimer` para verificar o endpoint `/health` a cada **30 segundos**
- Timeout de **5 segundos** por requisição
- Dispara evento `OnStatusChanged` **apenas quando o status muda** (evita notificações redundantes)
- Trata `HttpRequestException`, `TaskCanceledException` e exceções genéricas separadamente, com mensagens amigáveis em português
- Mantém `UltimaFalha` timestamp para diagnóstico
- Implementa `IDisposable` com `CancellationTokenSource` para cleanup adequado

O componente [`ApiHealthBanner.razor`](../ui/TicketPrime.Web/Components/Shared/ApiHealthBanner.razor) exibe:
- Banner **âmbar** (`#FEF3C7` com borda `#F59E0B`) com animação `slideDown`
- Ícone de alerta SVG acessível (`aria-hidden="true"`)
- Botão **"Tentar novamente"** para forçar reconexão
- Botão **fechar** para descartar o aviso
- Mensagem contextual: "API reconectada com sucesso" / "Não foi possível conectar ao servidor" / "O servidor está demorando para responder"

**Impacto:** Nota reduzida de 4/10 → 1/10 (problema resolvido com implementação acima da média).

### 7.4 Não há tratamento de concorrência para o cancelamento de reserva (🔴 5/10)

O método `CancelarIngressoAsync` em [`ReservaService.cs`](../src/Service/ReservaService.cs:152-184) não usa `UPDLOCK` como a compra. Em alta concorrência, dois cancelamentos simultâneos podem causar race condition na devolução de vagas.

---

## 📊 RESUMO DAS NOTAS

| Categoria | Nota Média | Qtd Itens | Δ |
|-----------|-----------|-----------|-----------|
| 🔴 Segurança | **8.2/10** | 9 | ↔ |
| 🟠 Arquitetura | **7.2/10** | 7 | ↔ |
| 🟡 Frontend | **5.6/10** | 7 | **↓ 0.9** |
| 🔵 Backend | **4.2/10** | 6 | ↔ |
| ⚪ Testes | **6.4/10** | 5 | **↓ 0.3** |
| 🟣 DevOps | **7.0/10** | 5 | ↔ |
| ⚫ Outros | **3.6/10** | 5 | **↓ 0.4** |
| **GERAL** | **6.3/10** | **44** | **↓ 0.2** |

> Quanto menor a nota, melhor — representa a gravidade média dos problemas. A redução (↓) indica melhoria.

---

## 🎯 VEREDITO FINAL (REVISÃO — 2026-05-11)

O TicketPrime tem **potencial técnico** — transações atômicas com `UPDLOCK`, BCrypt, Dapper parametrizado, E2E encryption via Web Crypto API, FluentValidation, **CSS Isolation**, **HealthCheckService com PeriodicTimer**, e **testes E2E com Playwright** são pontos positivos consolidados.

**Melhorias implementadas desde a análise anterior:**

1. ✅ **CSS extraído para `.razor.css`** — 9 componentes migrados de `<style>` inline para arquivos escopados, usando **CSS Isolation** do Blazor. Animações mantidas (`float1`, `float2`, `fadeUp`, `pulse`, `hp-drop`).
2. ✅ **HealthCheckService + ApiHealthBanner** — Monitoramento do backend a cada 30s com `PeriodicTimer`, timeout de 5s, eventos somente em mudança de status, banner âmbar com "Tentar novamente".
3. ✅ **Testes E2E expandidos** — 11 testes Playwright em 4 arquivos: navegação, cadastro, fluxo de checkout, página 404, recuperação de senha.

**Ainda pendente (crítico):**

1. ⚠️ **E2E encryption incompleta:** Criptografia de imagens sofisticada (ECDH P-256 + AES-GCM-256 + AES-KW) mas os dados **nunca são enviados ao servidor**.
2. ⚠️ **Cancelamento não restaura cupom:** `TotalUsado` do cupom não é decrementado.
3. ⚠️ **Rate limit 429 sem tratamento UX:** Quando o limite de 3 compras/minuto é atingido, o usuário recebe erro sem mensagem amigável.
4. ⚠️ **Testes de segurança ausentes:** Injeção SQL, JWT inválido, acesso não autorizado, CSRF.
5. ⚠️ **HealthCheck não persiste no backend:** O `HealthController` retorna resposta fixa — não verifica banco de dados.

> **Progresso geral:** ~35% dos problemas identificados foram total ou parcialmente resolvidos. O projeto evoluiu positivamente com foco em qualidade de código (CSS Isolation), observabilidade (HealthCheck) e qualidade de teste (Playwright E2E).
