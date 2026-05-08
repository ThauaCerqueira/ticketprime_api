# Análise de Problema: Criação de Eventos e Cupons Não Funciona

## Resumo Executivo
Os formulários de criação de eventos e cupons estão redirecionando para a página anterior sem criar registros. O problema é que as requisições HTTP POST não estão incluindo o token JWT de autenticação no header, causando erro 401 (Unauthorized).

---

## Problema Identificado

### 1. **Falta de Autenticação nas Requisições HTTP**
- **Localização**: Componentes Razor em `ui/TicketPrime.Web/Components/Pages/`
  - `CriarEvento.razor`
  - `CadastrodeCupom.razor`

- **Causa**: As requisições HTTP não incluem o Bearer token no header `Authorization`
  
- **Fluxo Atual**:
  1. Usuário faz login ✓
  2. Token JWT é armazenado em `SessionService` ✓
  3. Usuário acessa página de criar evento ✓
  4. Usuário preenche formulário ✓
  5. ❌ Ao submeter, a requisição é enviada SEM o token JWT
  6. API retorna 401 (Unauthorized) silenciosamente
  7. Formulário falha e redireciona

### 2. **HttpClient Não Configurado para Incluir Token**
- **Arquivo**: `ui/TicketPrime.Web/Program.cs` (linhas 29-30)
- **Problema**: O HttpClient é configurado apenas com `BaseAddress`, sem incluir a lógica de adicionar o token

```csharp
// ATUAL (SEM AUTORIZAÇÃO):
builder.Services.AddScoped(sp => new HttpClient { 
    BaseAddress = new Uri(apiBaseUrl) 
});

// NECESSÁRIO (COM AUTORIZAÇÃO):
// Precisa de um HttpMessageHandler customizado que adicione o token ao header
```

### 3. **API Requer Autorização**
- **Controllers**: `EventosController`, `CupomController`
- **Métodos protegidos**:
  - `EventosController.CriarEvento()` - `[Authorize(Roles = "ADMIN")]`
  - `CupomController.Criar()` - `[Authorize(Roles = "ADMIN")]`

---

## Requisitos para Solução

### R1: Criar HttpMessageHandler Customizado
- Deve interceptar requisições HTTP
- Deve adicionar Bearer token ao header `Authorization`
- Deve ler o token de `SessionService`

### R2: Registrar HttpClient com Handler
- Registrar em `Program.cs` usando `AddHttpClient()`
- Injetar `SessionService` para acessar o token

### R3: Atualizar Componentes Razor
- Modificar `CriarEvento.razor`
- Modificar `CadastrodeCupom.razor`
- Garantir que ambos usam o HttpClient configurado corretamente

### R4: Tratamento de Erros
- Exibir mensagem de erro quando 401 (Unauthorized)
- Exibir mensagem de erro genérica para outros status codes
- Facilitar debug com logs

### R5: Manter Compatibilidade
- Não quebrar outras requisições HTTP existentes
- Manter a URL base configurável (atual: `http://localhost:5164`)

---

## Arquivos Afetados

| Arquivo | Modificação |
|---------|-------------|
| `ui/TicketPrime.Web/Program.cs` | Criar AuthHttpClientHandler e registrar HttpClient |
| `ui/TicketPrime.Web/Services/SessionService.cs` | NENHUMA (já armazena token) |
| `ui/TicketPrime.Web/Components/Pages/CriarEvento.razor` | Melhorar tratamento de erros |
| `ui/TicketPrime.Web/Components/Pages/CadastrodeCupom.razor` | Melhorar tratamento de erros |

---

## Stack de Implementação

### Opção 1: HttpMessageHandler Customizado (RECOMENDADO)
```
Program.cs
  ├── Criar classe AuthHttpClientHandler : DelegatingHandler
  │   └── Interceptar requisição e adicionar token
  └── Registrar: builder.Services.AddHttpClient<...>()
       .AddHttpMessageHandler()
```

**Vantagens**:
- Centralizado e reutilizável
- Automático para todas as requisições
- Clean separation of concerns

**Desvantagens**:
- Requer criação de classe adicional

### Opção 2: Adicionar Token Manualmente (NÃO RECOMENDADO)
- Modificar cada componente Razor
- Adicionar linha em cada POST/PUT/DELETE

**Desvantagens**:
- Duplicação de código
- Fácil esquecer em novas requisições
- Difícil de manter

---

## Testes Esperados

### Teste 1: Criar Evento
1. Login com credenciais ADMIN ✓
2. Navegarara `/eventos/novo` ✓
3. Preencher formulário
4. Clicar "Criar evento"
5. **ESPERADO**: Mensagem de sucesso e redirecionamento para lista de eventos

### Teste 2: Criar Cupom
1. Login com credenciais ADMIN ✓
2. Navegar para `/cupons/cadastrar`
3. Preencher formulário
4. Clicar "Criar cupom"
5. **ESPERADO**: Mensagem de sucesso e redirecionamento para lista de cupons

### Teste 3: Sem Token
1. Acessar diretamente `/eventos/novo` (sem login)
2. Preencher formulário
3. Tentar criar evento
4. **ESPERADO**: Redirecionado para login (verificar `SessionService.EhAdmin`)

---

## Impacto

### Antes (Atual)
- ❌ Criar eventos: Não funciona
- ❌ Criar cupons: Não funciona
- ✓ Listar eventos: Funciona (sem autenticação)
- ✓ Login: Funciona

### Depois (Esperado)
- ✅ Criar eventos: Funciona
- ✅ Criar cupons: Funciona
- ✅ Listar eventos: Continua funcionando
- ✅ Login: Continua funcionando
- ✅ Mensagens de erro claras ao usuário

---

## Cronograma de Implementação

1. **Fase 1**: Criar AuthHttpClientHandler em Program.cs
2. **Fase 2**: Atualizar CriarEvento.razor com melhor tratamento de erros
3. **Fase 3**: Atualizar CadastrodeCupom.razor com melhor tratamento de erros
4. **Fase 4**: Testes manuais
5. **Fase 5**: Testes de segurança (401, 403)

---

## Observações

- A URL da API está corretamente configurada: `http://localhost:5164`
- SessionService já armazena o token corretamente
- A API rejeita requisições sem token (esperado)
- Os DTOs estão corretos
- O banco de dados está inicializando corretamente

---

## Referências

- JWT Bearer Authentication: [Microsoft Docs](https://learn.microsoft.com/pt-br/aspnet/core/security/authentication/bearer)
- HttpClientFactory: [Microsoft Docs](https://learn.microsoft.com/pt-br/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- Blazor Authentication: [Microsoft Docs](https://learn.microsoft.com/pt-br/aspnet/core/blazor/security/)
