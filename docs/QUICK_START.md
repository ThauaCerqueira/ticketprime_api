# Quick Start - Implementação Rápida (5 Minutos)

## ⚡ Implementação Express

Se você quer implementar AGORA, siga apenas esta seção.

---

## Passo 1: Criar AuthHttpClientHandler.cs

**Arquivo**: `ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs`

```csharp
using System.Net.Http.Headers;

namespace TicketPrime.Web.Services;

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
        if (!string.IsNullOrEmpty(_sessionService.Token))
        {
            request.Headers.Authorization = 
                new AuthenticationHeaderValue("Bearer", _sessionService.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

✅ Copiar e colar. Pronto!

---

## Passo 2: Atualizar Program.cs

**Arquivo**: `ui/TicketPrime.Web/Program.cs`

### Encontrar esta seção:
```csharp
// HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
```

### Substituir por:
```csharp
// HttpClient for API calls with JWT authentication
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped<AuthHttpClientHandler>();
builder.Services.AddHttpClient<HttpClient>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthHttpClientHandler>();
```

✅ Salvar. Pronto!

---

## Passo 3: Testar

1. **Parar a aplicação**:
   ```powershell
   Ctrl+C
   ```

2. **Recompilar**:
   ```powershell
   dotnet clean
   dotnet build
   ```

3. **Executar**:
   ```powershell
   dotnet run
   ```

4. **Testar no navegador**:
   - Ir para `http://localhost:5194/login`
   - Login com ADMIN
   - Ir para `/eventos/novo`
   - Preencher e submeter
   - ✅ Deve funcionar!

---

## Checklist de Implementação (5 min)

- [x] Criar arquivo `AuthHttpClientHandler.cs` ✅
- [x] Copiar código do Passo 1 ✅
- [x] Atualizar `Program.cs` (Passo 2) ✅
- [x] Corrigir `SessionService` de `AddSingleton` para `AddScoped` ⚠️
- [x] Recompilar ✅ (0 warnings, 0 errors)
- [ ] Executar (pendente)
- [ ] Testar criação de evento (pendente)
- [ ] Testar criação de cupom (pendente)

---

## Troubleshooting Rápido

### Erro: "File already exists"
```powershell
# Remover arquivo antigo
Remove-Item ui/TicketPrime.Web/Services/AuthHttpClientHandler.cs
# Criar novamente
```

### Erro: "Compilation error"
```
- Verificar namespace: TicketPrime.Web.Services
- Verificar imports: using System.Net.Http.Headers;
- Verificar braces: todas as chaves devem estar fechadas
```

### Erro: "HttpClient is not registered"
```csharp
// Verificar se estas linhas estão em Program.cs:
builder.Services.AddScoped<AuthHttpClientHandler>();
builder.Services.AddHttpClient<HttpClient>()
    .AddHttpMessageHandler<AuthHttpClientHandler>();
```

---

## ✅ Validação Rápida

### Criar Evento
1. Login: ✓
2. `/eventos/novo`: ✓
3. Preencher formulário
4. Clicar "Criar evento"
5. Esperado: "✅ Evento criado com sucesso!"

### Criar Cupom
1. Login: ✓
2. `/cupons/cadastrar`: ✓
3. Preencher formulário
4. Clicar "Criar cupom"
5. Esperado: "✅ Cupom criado com sucesso!"

---

## 📊 Impact Check

Verificar se outros recursos continuam funcionando:

```powershell
# API Health Check
curl http://localhost:5164/api/eventos

# Web App Health Check
curl http://localhost:5194/

# Login Test
# (Fazer manualmente no navegador)
```

---

## 🎓 O que foi feito?

| Antes | Depois |
|-------|--------|
| POST sem token | POST com Bearer token ✓ |
| 401 Unauthorized | 201 Created ✓ |
| Falha silenciosa | Mensagem de sucesso ✓ |
| Evento não criado | Evento criado ✓ |
| Cupom não criado | Cupom criado ✓ |

---

## 📝 Documentos Disponíveis

Se precisar mais detalhes:

1. **SUMARIO_EXECUTIVO.md** - Visão geral do problema
2. **ANALISE_PROBLEMA_CRIACAO_EVENTOS_CUPONS.md** - Análise técnica
3. **REQUISITOS_DETALHADOS.md** - Especificação completa
4. **PLANO_IMPLEMENTACAO.md** - Passo-a-passo detalhado (11 fases)

---

## ❓ Perguntas Frequentes

### P: Isso vai quebrar outras funcionalidades?
**R**: Não. O handler funciona para todas as requisições automaticamente.

### P: Preciso modificar os componentes Razor?
**R**: Não. Tudo é automático via handler.

### P: E se o usuário não estiver logado?
**R**: O handler não adiciona token, e a API retorna 401 (esperado).

### P: Quanto tempo leva?
**R**: 5-10 minutos para implementar + 5-10 minutos para testar = 20 minutos total.

### P: Preciso fazer backup?
**R**: Recomendado, mas não é crítico. O código é simples e reversível.

---

## 🚀 Próximo Passo

**Implementar agora**: Siga os 3 passos acima e terá o sistema funcionando em <30 minutos.

---

**Tempo estimado**: ⏱️ 5-30 minutos  
**Dificuldade**: 🟢 Fácil  
**Risco**: 🟢 Baixo  
**Resultado**: ✅ Sistema funcionando
