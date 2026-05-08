# Requisitos de Modificação - Sistema TicketPrime

## Documento de Requisitos Funcionais e Técnicos

---

## RF-001: Autenticação em Requisições HTTP

### Descrição
O sistema deve automaticamente incluir o token JWT de autenticação em todas as requisições HTTP da aplicação web para a API.

### Critério de Aceitação
- [x] Requisições POST, PUT, DELETE incluem header `Authorization: Bearer <token>` ✅
- [x] Header é adicionado automaticamente sem modificação do código em cada componente ✅
- [x] Se usuário não está logado (token vazio), requisição é enviada sem token (API retorna 401) ✅
- [x] Token é retirado do `SessionService` a cada requisição (token atualizado) ✅

### Prioridade
**CRÍTICA** - Bloqueia funcionalidade de criação de eventos e cupons

---

## RF-002: Criação de Eventos

### Descrição
Usuários com perfil ADMIN devem conseguir criar novos eventos preenchendo um formulário.

### Critério de Aceitação
- [x] Formulário aceita: Nome, Data, Capacidade, Preço ✅
- [x] Ao submeter, dados são enviados para `POST /api/eventos` ✅
- [x] Se sucesso (200): Exibir "Evento criado com sucesso!" e limpar formulário ✅
- [x] Se erro 401: Exibir "Não autorizado. Faça login novamente." ✅
- [x] Se erro 400: Exibir mensagem de erro da API ✅
- [x] Se erro de conexão: Exibir "Erro de conexão com o servidor" ✅
- [x] Após sucesso, usuário continua na página de criar evento (não redireciona automaticamente) ✅

### Prioridade
**ALTA** - Funcionalidade principal do sistema

---

## RF-003: Criação de Cupons

### Descrição
Usuários com perfil ADMIN devem conseguir criar novos cupons de desconto.

### Critério de Aceitação
- [x] Formulário aceita: Código, Desconto (%), Valor Mínimo ✅
- [x] Ao submeter, dados são enviados para `POST /api/cupons` ✅
- [x] Se sucesso (201): Exibir "Cupom criado com sucesso!" e limpar formulário ✅
- [x] Se erro 401: Exibir "Não autorizado. Faça login novamente." ✅
- [x] Se erro 400: Exibir mensagem de erro da API ✅
- [x] Se erro de conexão: Exibir "Erro de conexão com o servidor" ✅
- [x] Após sucesso, usuário continua na página de criar cupom (não redireciona automaticamente) ✅

### Prioridade
**ALTA** - Funcionalidade principal do sistema

---

## RT-001: HttpMessageHandler Customizado

### Descrição Técnica
Implementar um `DelegatingHandler` que intercepta requisições HTTP e adiciona o token JWT ao header `Authorization`.

### Especificação
```csharp
public class AuthHttpClientHandler : DelegatingHandler
{
    // Injetar SessionService
    // Sobrescrever SendAsync()
    // Adicionar header Authorization se token não vazio
    // Propagar requisição
}
```

### Requisitos Técnicos
- [x] Classe deve herdar de `DelegatingHandler` ✅
- [x] Injetar `SessionService` via construtor ✅
- [x] Ler `SessionService.Token` a cada requisição ✅
- [x] Se token não vazio: adicionar `Authorization: Bearer {token}` ✅
- [x] Se token vazio: não adicionar header (deixa API retornar 401) ✅
- [x] Propagar para handler seguinte (InnerHandler) ✅
- [x] Tratar exceções graciosamente ✅

### Localização
`ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs` (novo arquivo)

### Exemplo de Uso
```csharp
// Program.cs
builder.Services.AddScoped<SessionService>();
builder.Services.AddHttpClient<HttpClient>()
    .AddHttpMessageHandler(sp => new AuthHttpClientHandler(sp.GetRequiredService<SessionService>()));
```

---

## RT-002: Configuração do HttpClient em Program.cs

### Descrição Técnica
Atualizar a configuração do `HttpClient` em `Program.cs` para usar o novo `AuthHttpClientHandler`.

### Modificações Necessárias
- [x] Remover registro simples de HttpClient ✅
- [x] Adicionar `AddHttpClient<HttpClient>()` com handler customizado ✅
- [x] Garantir `BaseAddress` continua sendo configurada de `appsettings.json` ✅
- [x] Manter `ApiSettings:BaseUrl` em `appsettings.json` (já existe) ✅
- [x] Corrigir `SessionService` de `AddSingleton` para `AddScoped` ⚠️ (bug crítico)

### Antes
```csharp
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
```

### Depois
```csharp
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddHttpClient<HttpClient>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthHttpClientHandler>();

builder.Services.AddScoped<AuthHttpClientHandler>();
builder.Services.AddScoped<SessionService>();
```

---

## RT-003: Tratamento de Erros em CriarEvento.razor

### Descrição Técnica
Melhorar o tratamento de erros ao enviar requisição de criação de evento.

### Modificações Necessárias
- [x] Se HTTP 401: Exibir "Não autorizado" ✅
- [x] Se HTTP 400: Extrair mensagem de erro da resposta ✅
- [x] Se HTTP 500: Exibir "Erro interno do servidor" ✅
- [x] Se exception de conexão: Exibir "Erro de conexão" ✅
- [x] Manter estado do formulário se houver erro (não limpar) ✅
- [x] Adicionar log/console para debug ✅

### Status Code Esperados
| Code | Ação |
|------|------|
| 201 | Sucesso - "Evento criado com sucesso!" |
| 400 | Erro validação - Ler mensagem de erro |
| 401 | Não autorizado - "Não autorizado. Faça login novamente." |
| 500 | Erro servidor - "Erro interno do servidor" |

---

## RT-004: Tratamento de Erros em CadastrodeCupom.razor

### Descrição Técnica
Melhorar o tratamento de erros ao enviar requisição de criação de cupom.

### Modificações Necessárias
- [x] Se HTTP 201: Exibir "Cupom criado com sucesso!" ✅
- [x] Se HTTP 401: Exibir "Não autorizado" ✅
- [x] Se HTTP 400: Extrair mensagem de erro ✅
- [x] Se HTTP 409: Exibir "Cupom já existe" ✅
- [x] Se HTTP 500: Exibir "Erro interno do servidor" ✅
- [x] Manter estado do formulário se houver erro ✅
- [x] Adicionar log/console para debug ✅

### Status Code Esperados
| Code | Ação |
|------|------|
| 201 | Sucesso - "Cupom criado com sucesso!" |
| 400 | Erro validação - Ler mensagem de erro |
| 401 | Não autorizado - "Não autorizado. Faça login novamente." |
| 409 | Conflito - "Cupom já existe" |
| 500 | Erro servidor - "Erro interno do servidor" |

---

## RT-005: Validação no Frontend

### Descrição Técnica
Adicionar validações do lado do cliente para melhorar UX.

### Modificações Necessárias
- [x] CriarEvento.razor: Validar data (deve ser futura) ✅
- [x] CriarEvento.razor: Validar capacidade (maior que 0) ✅
- [x] CriarEvento.razor: Validar preço (maior que 0) ✅
- [x] CadastrodeCupom.razor: Validar código (não vazio, apenas alfanuméricos) ✅
- [x] CadastrodeCupom.razor: Validar desconto (1-100%) ✅
- [x] CadastrodeCupom.razor: Validar valor mínimo (>=0) ✅

---

## Dependências

### Dependências Internas
- `SessionService` - Armazena token JWT ✓ Já existe
- `CriarEventoDTO` - DTO para evento ✓ Já existe
- `CriarCupomDTO` - DTO para cupom ✓ Já existe

### Dependências Externas
- NuGet: Nenhuma dependência adicional (usar .NET padrão)

---

## Critérios de Teste

### Teste de Unidade
```
- [x] AuthHttpClientHandler adiciona token quando presente ✅
- [x] AuthHttpClientHandler não adiciona token quando vazio ✅
- [x] AuthHttpClientHandler preserva headers existentes ✅
```

### Teste de Integração
```
- [ ] POST /api/eventos com token: 201 (pendente teste manual)
- [ ] POST /api/eventos sem token: 401 (pendente teste manual)
- [ ] POST /api/cupons com token: 201 (pendente teste manual)
- [ ] POST /api/cupons sem token: 401 (pendente teste manual)
```

### Teste Manual (Smoke Test)
```
- [ ] Login como ADMIN (pendente)
- [ ] Criar evento com sucesso (pendente)
- [ ] Ver evento na lista (pendente)
- [ ] Criar cupom com sucesso (pendente)
- [ ] Ver cupom na lista (pendente)
- [ ] Logout (pendente)
- [ ] Tentar criar evento sem login: deve falhar (pendente)
```

---

## Arquitetura Proposta

```
┌─────────────────────────────────────────────────────────────┐
│                    Blazor Web App (5194)                    │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  CriarEvento.razor          CadastrodeCupom.razor           │
│         │                            │                      │
│         └────────────┬───────────────┘                      │
│                      │                                      │
│                   HttpClient (BaseAddress: 5164)            │
│                      │                                      │
│              AuthHttpClientHandler ◄─── SessionService      │
│          (adiciona Authorization header)                    │
│                      │                                      │
│    ┌─────────────────┴──────────────────┐                   │
│    │                                    │                   │
│  POST /api/eventos                  POST /api/cupons        │
│    │                                    │                   │
└────┼────────────────────────────────────┼──────────────────┘
     │                                    │
     ▼                                    ▼
┌─────────────────────────────────────────────────────────────┐
│              ASP.NET Core API (5164)                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  EventosController          CupomController                │
│   [Authorize(ADMIN)]         [Authorize(ADMIN)]             │
│    ✓ Cria evento             ✓ Cria cupom                   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## Estimativa de Esforço

| Tarefa | Tempo | Complexidade |
|--------|-------|-------------|
| Criar AuthHttpClientHandler | 15 min | Baixa |
| Atualizar Program.cs | 10 min | Baixa |
| Testar requisições | 20 min | Média |
| Melhorar tratamento de erros | 20 min | Baixa |
| Testes manuais | 15 min | Baixa |
| **TOTAL** | **~80 min** | **Média** |

---

## Riscos

| Risco | Probabilidade | Impacto | Mitigação |
|-------|--------------|--------|-----------|
| Token não ser lido de SessionService | Baixa | Alto | Testes unitários |
| Quebrar outras requisições HTTP | Baixa | Alto | Testar listagens |
| Header duplicado | Muito Baixa | Baixo | Verificar no navegador |
| CORS error | Média | Alto | Verificar CORS na API |

---

## Notas Adicionais

- A API já está com CORS configurado ✓
- O SessionService já armazena token corretamente ✓
- Os DTOs estão corretos ✓
- A autorização [Authorize] está configurada na API ✓
- Não há necessidade de modificar banco de dados
- Não há necessidade de adicionar dependências NuGet

---

## Aprovação

- [ ] Product Owner
- [ ] Tech Lead
- [ ] QA

---

**Data de Criação**: 2026-05-08  
**Versão**: 1.0  
**Status**: Pendente Aprovação
