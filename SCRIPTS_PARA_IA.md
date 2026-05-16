# 🤖 Script para IA — Correções Pendentes no TicketPrime

**Instruções:** Copie e cole este documento inteiro para a IA (ex: GitHub Copilot, ChatGPT, Claude).
Cada seção contém um problema com o **arquivo exato**, **código para localizar** e **código para substituir**.

---

## 📋 Índice dos Problemas

| # | Problema | Arquivo | Gravidade |
|---|----------|---------|-----------|
| 1 | Over-posting no cadastro de usuário | `src/Controllers/UsuarioController.cs` + `src/DTOs/CreateUserDto.cs` | 🔴 |
| 2 | Blacklist ingênua de validação de email | `src/Service/UsuarioService.cs` | 🔴 |
| 3 | script.sql na imagem Docker de produção | `src/Dockerfile` | 🔴 |
| 4 | Dependências de teste (xUnit) no csproj de produção | `src/src.csproj` | 🟠 |
| 5 | Chaves cripto perdidas no restart (CryptoKeyService) | `src/Service/CryptoKeyService.cs` | 🟠 |
| 6 | Cupom deletado sem tratar FK violation | `src/Controllers/CupomController.cs` + `src/Infrastructure/Repository/CupomRepository.cs` | 🟠 |
| 7 | Timeout genérico de 30s atrapalha relatórios | `src/Infrastructure/DbConnectionFactory.cs` | 🟡 |

---

## 🔴 1 — Over-Posting no Cadastro de Usuário

### Arquivos
- `src/Controllers/UsuarioController.cs`
- `src/DTOs/CreateUserDto.cs`

### Problema
O `UserController.Cadastrar()` aceita o model `User` diretamente via `[FromBody]`. O model `User` contém campos sensíveis como `Perfil`, `SenhaTemporaria`, `EmailVerificado`, `Slug`. Embora o Perfil seja sobrescrito, se alguém adicionar um novo campo no futuro e esquecer de sobrescrever, vira over-posting.

### O que fazer

**1a — Expandir `CreateUserDto.cs`** para conter todos os campos necessários no cadastro:

Arquivo: `src/DTOs/CreateUserDto.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace src.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "O CPF é obrigatório")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "O CPF deve ter exatamente 11 dígitos")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "O CPF deve conter apenas números")]
    public required string Cpf { get; set; }

    [Required(ErrorMessage = "O Nome é obrigatório")]
    [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres")]
    public required string Nome { get; set; }

    [Required(ErrorMessage = "O Email é obrigatório")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "A Senha é obrigatória")]
    [MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres")]
    public required string Senha { get; set; }
}
```

**1b — Modificar `UsuarioController.cs`** para usar `CreateUserDto`:

No método `Cadastrar()`, trocar a assinatura de `[FromBody] User usuario` para `[FromBody] CreateUserDto dto`.

Depois, mapear o DTO para o model User dentro do método:
```csharp
var usuario = new User
{
    Cpf = dto.Cpf,
    Nome = dto.Nome,
    Email = dto.Email,
    Senha = dto.Senha,
    Perfil = "CLIENTE"  // Segurança: sempre força CLIENTE
};
```

---

## 🔴 2 — Blacklist Ingênua de Validação de Email

### Arquivo
`src/Service/UsuarioService.cs`

### Problema
O método `ValidarEmail()` usa blacklist case-sensitive:
- `"EXEC "` passa (não tem `StringComparison.OrdinalIgnoreCase`)
- `"joao@select.com.br"` é rejeitado (falso positivo)
- Blacklist é frágil — basta `"selECt"` para burlar

### O que fazer

**2a — NO ARQUIVO `src/Service/UsuarioService.cs`:**

Encontrar o método `ValidarEmail()` (linha ~71) e substituir por este código mais seguro:

```csharp
private static readonly Regex EmailInjectionRegex = new(
    @"[<>"";'\\]",
    RegexOptions.Compiled);

private static void ValidarEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        throw new ArgumentException("O email é obrigatório.");

    // Rejeita apenas caracteres que são comprovadamente perigosos em qualquer contexto
    // Dapper já protege contra SQL injection via parâmetros nomeados (@param).
    // Esta validação é uma camada adicional de defesa, não a principal.
    if (EmailInjectionRegex.IsMatch(email))
        throw new ArgumentException("O email informado contém caracteres inválidos.");

    if (!EmailValidoRegex.IsMatch(email))
        throw new ArgumentException("O email informado não possui um formato válido.");
}
```

Adicionar o campo `EmailInjectionRegex` junto com os outros `static readonly Regex` no topo da classe `UserService`.

---

## 🔴 3 — script.sql na Imagem Docker de Produção

### Arquivo
`src/Dockerfile`

### Problema
A linha `COPY db/script.sql db/script.sql` coloca o schema completo do banco (com hash da senha admin) dentro da imagem de produção. `docker exec cat db/script.sql` expõe tudo.

### O que fazer

**3a — NO ARQUIVO `src/Dockerfile`:**

SUBSTITIR:
```dockerfile
COPY --from=build /publish .
# Copia o script de banco para que InitializeDatabase encontre db/script.sql em runtime
COPY db/script.sql db/script.sql
```

POR:
```dockerfile
COPY --from=build /publish .
# ═══════════════════════════════════════════════════════════════════
# SEGURANÇA: script.sql NÃO é copiado para a imagem de produção.
#   Em produção, o banco já deve estar criado e migrado.
#   InitializeDatabase() só deve rodar em Development.
#   Use migrations versionadas (DbUp/Flyway) para produção.
# ═══════════════════════════════════════════════════════════════════
```

**3b — NO ARQUIVO `src/Program.cs`:**

Encontrar o método `InitializeDatabase()` e adicionar no início:
```csharp
private static void InitializeDatabase(string connectionString)
{
    // ═══════════════════════════════════════════════════════════════
    // SEGURANÇA: Não executa script.sql em produção.
    //   Em produção, o banco já deve estar provisionado via
    //   migrations versionadas (DbUp/Flyway) ou scripts manuais.
    //   Este método só executa em Development.
    // ═══════════════════════════════════════════════════════════════
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    if (env == "Production")
    {
        Console.WriteLine("[DB Init] Production mode detected. Skipping database initialization.");
        return;
    }
    
    // Restante do código atual...
```

---

## 🟠 4 — Dependências de Teste no csproj de Produção

### Arquivo
`src/src.csproj`

### Problema
O projeto `src.csproj` (produção) tem referências a:
- `xunit` v2.9.3
- `xunit.runner.visualstudio` v3.1.5
- `Microsoft.NET.Test.Sdk` v18.4.0
- `coverlet.collector` v8.0.1

Isso aumenta o deploy em ~5MB e expõe test runners em produção.

### O que fazer

NO ARQUIVO `src/src.csproj`:

REMOVER estas linhas do `<ItemGroup>`:
```xml
<PackageReference Include="coverlet.collector" Version="8.0.1">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.4.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

---

## 🟠 5 — Chaves Cripto Perdidas no Restart (CryptoKeyService)

### Arquivo
`src/Service/CryptoKeyService.cs`

### Problema
As chaves ECDH P-256 de eventos estão em um `ConcurrentDictionary<int, (ECDiffieHellman Key, string Jwk)>` em memória.
- Se a API reiniciar, TODAS as chaves são perdidas
- Fotos criptografadas se tornam permanentemente ilegíveis
- Não replica entre instâncias

### O que fazer

**Solução simplificada (sem infraestrutura externa):**

NO ARQUIVO `src/Service/CryptoKeyService.cs`:

1. Adicionar dependência de `IConfiguration` no construtor
2. Salvar a chave privada em Base64 no `appsettings` (ou variável de ambiente) em vez de gerar na memória:

```csharp
private const string ChavePrivadaConfigKey = "Crypto:PrivateKeyBase64";

public CryptoKeyService(IConfiguration configuration)
{
    var chaveBase64 = configuration[ChavePrivadaConfigKey];
    
    if (!string.IsNullOrEmpty(chaveBase64))
    {
        // Restaura chave existente de configuração persistente
        var keyBytes = Convert.FromBase64String(chaveBase64);
        _ecdhGlobal = ECDiffieHellman.Create();
        _ecdhGlobal.ImportSubjectPublicKeyInfo(keyBytes, out _);
    }
    else
    {
        // Gera nova chave (primeira execução)
        _ecdhGlobal = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = _ecdhGlobal.ExportSubjectPublicKeyInfo();
        var base64 = Convert.ToBase64String(publicKey);
        Console.WriteLine($"[Crypto] NOVA CHAVE GERADA. Salve em appsettings: \"{ChavePrivadaConfigKey}\": \"{base64}\"");
    }
    
    _chavePublicaGlobalJwk = ExportarChavePublicaJwk(_ecdhGlobal);
}
```

> **Nota:** Para produção real, o ideal é usar Azure Key Vault, AWS KMS, ou HashiCorp Vault.

---

## 🟠 6 — Cupom Deletado sem Tratar FK Violation

### Arquivos
- `src/Controllers/CupomController.cs`
- `src/Infrastructure/Repository/CupomRepository.cs`

### Problema
Se um cupom com reservas ativas for deletado, o `DELETE FROM Cupons` viola a FK `FK_Reservas_Cupons` e lança um `SqlException` que vira erro 500 genérico.

### O que fazer

**6a — NO ARQUIVO `src/Infrastructure/Repository/CupomRepository.cs`:**

Substituir o método `DeletarAsync`:

```csharp
public async Task<(bool Sucesso, string? MensagemErro)> DeletarAsync(string codigo)
{
    using var connection = _connectionFactory.CreateConnection();
    try
    {
        var rows = await connection.ExecuteAsync(
            "DELETE FROM Cupons WHERE Codigo = @Codigo;",
            new { Codigo = codigo });
        return (rows > 0, null);
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 547)
    {
        // 547 = FOREIGN KEY constraint violation
        return (false, "Este cupom não pode ser excluído porque está vinculado a uma ou mais reservas ativas.");
    }
}
```

**6b — Depois**, atualizar a interface `ICupomRepository` para refletir a nova assinatura.

**6c — NO ARQUIVO `src/Controllers/CupomController.cs`:**

No método `Deletar()`, trocar para usar a nova assinatura e mostrar a mensagem amigável:

```csharp
public async Task<IResult> Deletar(string codigo)
{
    var (removido, erro) = await _couponService.DeletarAsync(codigo);
    if (removido)
        return Results.NoContent();
    if (!string.IsNullOrEmpty(erro))
        return Results.Conflict(new { mensagem = erro });
    return Results.NotFound(new { mensagem = "Cupom não encontrado." });
}
```

---

## 🟡 7 — Timeout Genérico de 30s Atrapalha Relatórios

### Arquivo
`src/Infrastructure/DbConnectionFactory.cs`

### Problema
`DefaultCommandTimeout = 30` é herdado por TODAS as queries. Relatórios financeiros, CSV export e dashboard analítico podem levar mais de 30s com muitos dados.

### O que fazer

NO ARQUIVO `src/Infrastructure/DbConnectionFactory.cs`:

Adicionar um timeout diferenciado para operações analíticas:

```csharp
/// <summary>
/// Tempo limite padrão (em segundos) para comandos SQL via Dapper.
/// 30s — evita que queries lentas seguem conexões no pool.
/// </summary>
public const int DefaultCommandTimeout = 30;

/// <summary>
/// Tempo limite para queries analíticas (relatórios, dashboards, CSV export).
/// 300s (5 min) — relatórios financeiros podem processar muitos dados.
/// </summary>
public const int AnalyticsCommandTimeout = 300;
```

Depois, nos repositórios que fazem queries analíticas (`ReservaRepository.ObterVendasPorPeriodoAsync`, `ObterRelatorioFinanceiroAsync`, etc.), passar `commandTimeout: DbConnectionFactory.AnalyticsCommandTimeout` nos métodos `QueryAsync`/`QuerySingleAsync`.

---

## 🚀 Como Usar

1. Copie este documento inteiro
2. Cole para a IA (Copilot, ChatGPT, Claude)
3. Peça: **"Aplique todas as correções abaixo no meu projeto TicketPrime"**
4. A IA vai modificar os arquivos um por um

> **Dica:** Peça para a IA aplicar as correções **uma por uma** e ir mostrando o diff, para você revisar antes de aceitar.
