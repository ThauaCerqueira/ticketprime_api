# Plano de Implementação - TicketPrime

## Resumo
Implementação de autenticação JWT automática em requisições HTTP para corrigir problema de criação de eventos e cupons.

---

## Fase 1: Preparação

### 1.1 Backup
- [ ] Fazer backup de `ui/TicketPrime.Web/Program.cs`
- [ ] Fazer backup de `ui/TicketPrime.Web/Components/Pages/CriarEvento.razor`
- [ ] Fazer backup de `ui/TicketPrime.Web/Components/Pages/CadastrodeCupom.razor`

### 1.2 Verificação de Pré-requisitos
- [ ] SessionService já existe ✓
- [ ] Componentes Razor já existem ✓
- [ ] API está funcionando ✓
- [ ] Autorização [Authorize] está na API ✓

---

## Fase 2: Implementação - Criar AuthHttpClientHandler

### 2.1 Criar novo arquivo
**Arquivo**: `ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs`

**Código**:
```csharp
using System.Net.Http.Headers;

namespace TicketPrime.Web.Services;

/// <summary>
/// Adiciona automaticamente o token JWT ao header Authorization de todas as requisições HTTP.
/// </summary>
public class AuthHttpClientHandler : DelegatingHandler
{
    private readonly SessionService _sessionService;

    public AuthHttpClientHandler(SessionService sessionService)
    {
        _sessionService = sessionService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Adicionar token ao header se usuário está logado
        if (!string.IsNullOrEmpty(_sessionService.Token))
        {
            request.Headers.Authorization = 
                new AuthenticationHeaderValue("Bearer", _sessionService.Token);
        }

        // Propagar requisição
        return await base.SendAsync(request, cancellationToken);
    }
}
```

**Checklist**:
- [ ] Arquivo criado em `ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs`
- [ ] Namespace correto: `TicketPrime.Web.Services`
- [ ] Herança: `DelegatingHandler`
- [ ] Método `SendAsync` implementado
- [ ] Token lido de `SessionService.Token`
- [ ] Header `Authorization` adicionado corretamente

---

## Fase 3: Implementação - Atualizar Program.cs

### 3.1 Localizar linha de registro de HttpClient
**Arquivo**: `ui/TicketPrime.Web/Program.cs`

**Localização**: Linhas 27-30 (aproximadamente)

**Código ATUAL**:
```csharp
// HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
```

### 3.2 Substituir por novo código
**Código NOVO**:
```csharp
// HttpClient for API calls with JWT authentication
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped<AuthHttpClientHandler>();
builder.Services.AddHttpClient<HttpClient>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthHttpClientHandler>();
```

**Checklist**:
- [ ] Linha antiga removida
- [ ] `AddScoped<AuthHttpClientHandler>()` adicionado
- [ ] `AddHttpClient<HttpClient>()` adicionado
- [ ] `ConfigureHttpClient` configura BaseAddress
- [ ] `AddHttpMessageHandler<AuthHttpClientHandler>()` adicionado
- [ ] Nenhuma linha foi removida acidentalmente

---

## Fase 4: Testes Unitários (Opcional)

### 4.1 Teste de AuthHttpClientHandler

**Arquivo**: `tests/AuthHttpClientHandlerTests.cs` (novo, OPCIONAL)

```csharp
using Xunit;
using Moq;
using TicketPrime.Web.Services;
using System.Net.Http.Headers;

public class AuthHttpClientHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsAuthorizationHeader_WhenTokenExists()
    {
        // Arrange
        var sessionServiceMock = new Mock<SessionService>();
        sessionServiceMock.Setup(s => s.Token).Returns("test-token-123");
        
        var handler = new AuthHttpClientHandler(sessionServiceMock.Object);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5164/api/eventos");

        // Act
        // (Simular envio via handler)
        
        // Assert
        // Verificar que header foi adicionado
    }

    [Fact]
    public async Task SendAsync_NoHeader_WhenTokenIsEmpty()
    {
        // Arrange
        var sessionServiceMock = new Mock<SessionService>();
        sessionServiceMock.Setup(s => s.Token).Returns(string.Empty);
        
        var handler = new AuthHttpClientHandler(sessionServiceMock.Object);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5164/api/eventos");

        // Act
        // (Simular envio via handler)
        
        // Assert
        // Verificar que nenhum header foi adicionado
    }
}
```

**Checklist** (OPCIONAL):
- [ ] Teste para token presente
- [ ] Teste para token vazio
- [ ] Teste para token null

---

## Fase 5: Testes Manuais - Criar Evento

### 5.1 Setup
- [ ] API rodando em `http://localhost:5164`
- [ ] Web app rodando em `http://localhost:5194`
- [ ] Navegar para `http://localhost:5194/login`

### 5.2 Login
- [ ] Inserir CPF: `123.456.789-00` (ou usuário ADMIN existente)
- [ ] Inserir Senha: `senha123` (ou senha correta)
- [ ] Clicar "Entrar"
- [ ] Verificar se redirecionou para home

### 5.3 Criar Evento
- [ ] Clicar em "Administrador" na navbar
- [ ] Clicar em "Novo Evento"
- [ ] Preencher:
  - Nome: "Teste BTS"
  - Data: `2026-06-15`
  - Capacidade: `1000`
  - Preço: `150.00`
- [ ] Clicar "Criar evento"
- [ ] **ESPERADO**: Mensagem "✅ Evento criado com sucesso!"
- [ ] **VERIFICAR**: Formulário continua vazio, pronto para novo evento

### 5.4 Verificar no Banco
- [ ] Executar query no SQL Server:
```sql
SELECT TOP 5 * FROM Evento ORDER BY Id DESC;
```
- [ ] Verificar que o evento "Teste BTS" foi criado

### Checklist:
- [ ] Login funciona
- [ ] Acesso a /eventos/novo funciona
- [ ] Formulário preenche sem erros
- [ ] Submissão não gera erro
- [ ] Mensagem de sucesso aparece
- [ ] Evento aparece no banco de dados

---

## Fase 6: Testes Manuais - Criar Cupom

### 6.1 Setup
- [ ] Usuário ADMIN logado (da fase anterior)
- [ ] Navegar para `http://localhost:5194/cupons/cadastrar`

### 6.2 Criar Cupom
- [ ] Preencher:
  - Código: `TEST10`
  - Desconto: `10`
  - Valor Mínimo: `50.00`
- [ ] Clicar "Criar cupom"
- [ ] **ESPERADO**: Mensagem "✅ Cupom criado com sucesso!"
- [ ] **VERIFICAR**: Formulário continua vazio, pronto para novo cupom

### 6.3 Verificar no Banco
- [ ] Executar query no SQL Server:
```sql
SELECT TOP 5 * FROM Cupom ORDER BY Id DESC;
```
- [ ] Verificar que o cupom "TEST10" foi criado

### Checklist:
- [ ] Acesso a /cupons/cadastrar funciona
- [ ] Formulário preenche sem erros
- [ ] Submissão não gera erro
- [ ] Mensagem de sucesso aparece
- [ ] Cupom aparece no banco de dados

---

## Fase 7: Testes de Erro

### 7.1 Erro 401 (Não Autorizado)
- [ ] Fazer logout
- [ ] Ir para `http://localhost:5194/eventos/novo`
- [ ] Preencher formulário
- [ ] Clicar "Criar evento"
- [ ] **ESPERADO**: Não criar evento (falha silenciosa ou redirecionamento para login)

### 7.2 Erro de Validação
- [ ] Fazer login novamente
- [ ] Ir para `http://localhost:5194/eventos/novo`
- [ ] Deixar campos em branco
- [ ] Clicar "Criar evento"
- [ ] **ESPERADO**: Mensagem de erro: "Campo obrigatório" ou similar

### 7.3 Erro de Conexão
- [ ] Parar API (`Ctrl+C` no terminal da API)
- [ ] Ir para `http://localhost:5194/eventos/novo`
- [ ] Preencher formulário
- [ ] Clicar "Criar evento"
- [ ] **ESPERADO**: Mensagem "Erro de conexão com o servidor"

### Checklist:
- [ ] Sem token: requisição é rejeitada
- [ ] Validação: erros mostram mensagem apropriada
- [ ] Sem API: erro de conexão é tratado

---

## Fase 8: Testes de Regressão

### 8.1 Verificar Funcionalidades Existentes

**Listar Eventos**:
- [ ] Navegar para `http://localhost:5194/eventos`
- [ ] Verificar lista de eventos carrega
- [ ] Verificar eventos criados aparecem na lista

**Listar Cupons** (se implementado):
- [ ] Navegar para `http://localhost:5194/cupons`
- [ ] Verificar lista de cupons carrega
- [ ] Verificar cupons criados aparecem na lista

**Home**:
- [ ] Navegar para `http://localhost:5194/`
- [ ] Verificar página carrega sem erros

**Logout**:
- [ ] Clicar no botão de logout
- [ ] Verificar redirecionamento para login

### Checklist:
- [ ] Listar eventos: OK
- [ ] Listar cupons: OK
- [ ] Home: OK
- [ ] Logout: OK

---

## Fase 9: Análise de Performance

### 9.1 Verificar Logs
- [ ] No terminal da API: verificar se há erros 401
- [ ] No navegador (F12 > Network): verificar requisições HTTP
- [ ] Verificar status das requisições POST

### 9.2 Benchmark Rápido
- [ ] Medir tempo para criar evento
- [ ] Comparar com benchmark anterior (se houver)
- [ ] **ESPERADO**: <2 segundos

### Checklist:
- [ ] Nenhum erro 401 nos logs
- [ ] Requisições têm header Authorization
- [ ] Performance aceitável

---

## Fase 10: Documentação

### 10.1 Atualizar Código
- [ ] Adicionar comentários em `AuthHttpClientHandler`
- [ ] Adicionar comentários em modificações do `Program.cs`

### 10.2 Documentação de Usuário
- [ ] Criar guia: "Como criar eventos"
- [ ] Criar guia: "Como criar cupons"
- [ ] Adicionar screenshots

### Checklist:
- [ ] Código comentado
- [ ] Documentação de usuário criada

---

## Fase 11: Deploy (Produção)

### 11.1 Backup de Produção
- [ ] Backup do banco de dados
- [ ] Backup dos arquivos web

### 11.2 Deploy
- [ ] Parar aplicação em produção
- [ ] Copiar novos arquivos
- [ ] Iniciar aplicação
- [ ] Verificar se está rodando

### 11.3 Verificação Pós-Deploy
- [ ] Testar criação de evento
- [ ] Testar criação de cupom
- [ ] Verificar logs de erro

### Checklist:
- [ ] Backup realizado
- [ ] Deploy bem-sucedido
- [ ] Testes em produção passam

---

## Rollback Plan

Se algo der errado:

1. **Parar a aplicação**:
   ```powershell
   Ctrl+C
   ```

2. **Restaurar arquivos do backup**:
   ```powershell
   Copy-Item -Path "Program.cs.bak" -Destination "Program.cs" -Force
   ```

3. **Reiniciar**:
   ```powershell
   dotnet run
   ```

4. **Verificar status**:
   - Tentar login
   - Verificar se aplicação está rodando
   - Revisar logs

---

## Checklist Final

### Código
- [ ] `AuthHttpClientHandler.cs` criado
- [ ] `Program.cs` atualizado
- [ ] Sem erros de compilação
- [ ] Sem warnings

### Testes
- [ ] Testes manuais: Criar evento OK
- [ ] Testes manuais: Criar cupom OK
- [ ] Testes manuais: Listar eventos OK
- [ ] Testes de erro OK
- [ ] Testes de regressão OK

### Documentação
- [ ] Arquivo ANALISE_PROBLEMA_CRIACAO_EVENTOS_CUPONS.md criado
- [ ] Arquivo REQUISITOS_DETALHADOS.md criado
- [ ] Arquivo PLANO_IMPLEMENTACAO.md criado (este)

### Deploy
- [ ] Backup realizado
- [ ] Deploy bem-sucedido
- [ ] Testes em produção passam

---

## Contatos

| Papel | Contato | Disponibilidade |
|-------|---------|-----------------|
| Product Owner | - | - |
| Tech Lead | - | - |
| QA | - | - |
| DevOps | - | - |

---

## Histórico de Versões

| Versão | Data | Autor | Mudanças |
|--------|------|-------|----------|
| 1.0 | 2026-05-08 | Bot | Versão inicial |

---

**Status**: 🟡 Pronto para Implementação  
**Próximo Passo**: Aprovação de Requisitos
