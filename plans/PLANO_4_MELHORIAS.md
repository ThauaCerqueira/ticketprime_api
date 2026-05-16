# 📋 PLANO DE EXECUÇÃO — 4 Melhorias Críticas

> **Instruções:** Cada tópico tem tarefas detalhadas com arquivos, códigos e comandos.
> Marque com ✅ conforme for concluindo.

---

## 🏷️ Tópico 1 — Rotacionar Senha SA + Usar User-Secrets

**Gravidade:** 🔴 Crítico | **Esforço:** ⭐ Médio (30 min)

### Contexto
A senha do SA (`TicketPrime@2024!`) está em texto puro no `src/appsettings.Development.json`, que já foi commitado no git e até tentaram remover com `git filter-branch` (falhou). Precisa:
1. Trocar a senha no banco
2. Remover do git de vez
3. Migrar para `dotnet user-secrets`

### Tarefas

- [ ] **1.1 — Gerar nova senha segura**
  ```bash
  # Gera uma senha de 32 caracteres
  $env:NOVA_SENHA = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % { [char]$_ })
  Write-Host "Nova senha: $env:NOVA_SENHA"
  ```

- [ ] **1.2 — Trocar senha no SQL Server**
  ```sql
  -- Conectar no SQL Server com SSMS ou sqlcmd
  -- No banco master:
  ALTER LOGIN sa WITH PASSWORD = 'NovaSenhaAqui123!';
  ```

- [ ] **1.3 — Configurar user-secrets no projeto**
  ```bash
  cd src
  dotnet user-secrets init
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=TicketPrime;User Id=sa;Password=NovaSenhaAqui123!;TrustServerCertificate=True;"
  dotnet user-secrets set "Jwt:Key" "ChaveJwtComPeloMenos32CaracteresAqui!!"
  ```

- [ ] **1.4 — Limpar appsettings.Development.json**
  ```json
  {
    "Logging": {
      "LogLevel": {
        "Default": "Debug",
        "Microsoft.AspNetCore": "Information"
      }
    },
    "Jwt": {
      "ExpireMinutes": 60
    }
  }
  ```
  > ⚠ Manter APENAS `Logging` e `Jwt:ExpireMinutes`. Remover `ConnectionStrings`, `EmailSettings`, `Jwt:Key`.

- [ ] **1.5 — Forçar git a esquecer o arquivo (tentativa 2)**
  ```bash
  # Remove do tracking (mas mantém local)
  git rm --cached src/appsettings.Development.json
  
  # Se quiser tentar remover do histórico de novo (CUIDADO: destrutivo)
  # git filter-branch --force --index-filter \
  #   "git rm --cached --ignore-unmatch src/appsettings.Development.json" \
  #   --prune-empty --tag-name-filter cat -- --all
  ```

- [ ] **1.6 — Documentar setup para novo dev**
  Criar `SETUP_DEV.md`:
  ```bash
  # Primeira vez clonando o projeto:
  cd src
  dotnet user-secrets init
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
  dotnet user-secrets set "Jwt:Key" "..."
  ```

---

## 🏷️ Tópico 2 — Criptografar ChavePix em Repouso

**Gravidade:** 🟠 Alto | **Esforço:** ⭐⭐⭐ Médio-Alto (2-3h)

### Contexto
A coluna `Reservas.ChavePix` armazena a chave Pix copia-e-cola em texto puro. Se o banco vazar, todas as chaves Pix ficam expostas.

**Arquivos envolvidos:**
- `db/script.sql` (schema)
- `src/Models/Reservation.cs`
- `src/Infrastructure/Repository/ReservaRepository.cs` (INSERT/UPDATE)
- `src/Service/ReservaService.cs` (onde `ChavePix` é atribuída)
- `src/Service/MercadoPagoPaymentGateway.cs` (origem do dado)

### Tarefas

- [ ] **2.1 — Criar serviço de criptografia Pix**
  **Arquivo:** `src/Service/PixCryptoService.cs`
  ```csharp
  using System.Security.Cryptography;
  
  namespace src.Service;
  
  /// <summary>
  /// Criptografa/descriptografa ChavePix em repouso usando AES-256-GCM.
  /// A chave mestra vem de IConfiguration (ou Vault).
  /// </summary>
  public sealed class PixCryptoService
  {
      private readonly byte[] _masterKey;
      private const string ConfigKey = "Crypto:PixMasterKey";
  
      public PixCryptoService(IConfiguration configuration)
      {
          var keyBase64 = configuration[ConfigKey];
          if (string.IsNullOrEmpty(keyBase64))
          {
              // Gera chave na primeira execução (logar para salvar)
              _masterKey = RandomNumberGenerator.GetBytes(32);
              Console.WriteLine($"[PixCrypto] NOVA CHAVE GERADA. Salve em {ConfigKey}: " +
                  Convert.ToBase64String(_masterKey));
          }
          else
          {
              _masterKey = Convert.FromBase64String(keyBase64);
          }
      }
  
      /// <summary>Nonce de 12 bytes + ciphertext + tag = total ~44 bytes Base64</summary>
      public string Encrypt(string plainText)
      {
          if (string.IsNullOrEmpty(plainText)) return string.Empty;
          var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
          var nonce = RandomNumberGenerator.GetBytes(12);
          var ciphertext = new byte[plainBytes.Length];
          var tag = new byte[16];
  
          using var aes = new AesGcm(_masterKey);
          aes.Encrypt(nonce, plainBytes, ciphertext, tag);
  
          // Formato: nonce + ciphertext + tag
          var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
          nonce.CopyTo(result, 0);
          ciphertext.CopyTo(result, nonce.Length);
          tag.CopyTo(result, nonce.Length + ciphertext.Length);
  
          return Convert.ToBase64String(result);
      }
  
      public string Decrypt(string encryptedBase64)
      {
          if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;
          var data = Convert.FromBase64String(encryptedBase64);
          var nonce = data[..12];
          var ciphertext = data[12..^16];
          var tag = data[^16..];
          var plainBytes = new byte[ciphertext.Length];
  
          using var aes = new AesGcm(_masterKey);
          aes.Decrypt(nonce, ciphertext, tag, plainBytes);
  
          return System.Text.Encoding.UTF8.GetString(plainBytes);
      }
  }
  ```

- [ ] **2.2 — Registrar no DI**
  **Arquivo:** `src/Program.cs`
  ```csharp
  builder.Services.AddSingleton<PixCryptoService>();
  ```
  Inserir junto com os outros `AddSingleton` (perto do `CryptoKeyService`).

- [ ] **2.3 — Criptografar ao salvar ChavePix**
  **Arquivo:** `src/Service/ReservaService.cs`
  **Local:** Onde `reserva.ChavePix = payResult.ChavePix;` (~linha 220)
  ```csharp
  // Para PIX, captura o QR Code e CRIPTOGRAFA antes de persistir
  reserva.ChavePix = !string.IsNullOrEmpty(payResult.ChavePix)
      ? _pixCryptoService.Encrypt(payResult.ChavePix)
      : null;
  ```
  > ⚠ Precisa adicionar `PixCryptoService` como dependência no construtor.

- [ ] **2.4 — Descriptografar ao exibir**
  **Arquivo:** Onde a ChavePix é retornada pro frontend (ex: `ReservationController.Comprar` ou `ReservaRepository`).
  > Se a ChavePix só é exibida uma vez (no response da compra), descriptografar antes de retornar.

- [ ] **2.5 — Adicionar chave mestra no Vault**
  ```bash
  vault kv patch secret/ticketprime/crypto PixMasterKey="<base64>"
  ```

---

## 🏷️ Tópico 3 — Webhook Validator: Warning em vez de Fail

**Gravidade:** 🟠 Alto | **Esforço:** ⭐ Fácil (15 min)

### Contexto
Se `MercadoPago:WebhookSecret` não for configurado, o `IsValid()` retorna `false` e **TODO webhook é rejeitado**. Em desenvolvimento com `SimulatedPaymentGateway`, o desenvolvedor nunca percebe que o webhook real não funciona.

### Tarefas

- [ ] **3.1 — Modificar `IsValid()` para emitir warning em vez de fail**
  **Arquivo:** `src/Service/MercadoPagoWebhookValidator.cs`
  
  **Substituir** o bloco inicial:
  ```csharp
  public bool IsValid(string? signatureHeader, string requestBody)
  {
      if (string.IsNullOrWhiteSpace(_webhookSecret))
      {
          _logger.LogWarning(
              "MercadoPago:WebhookSecret não configurado. " +
              "A validação de webhook está DESABILITADA — risco de segurança! " +
              "Configure via variável de ambiente MercadoPago__WebhookSecret.");
          return false;  // <-- ANTES: retorna false = webhook nunca funciona
      }
  ```
  
  **Por:**
  ```csharp
  public bool IsValid(string? signatureHeader, string requestBody)
  {
      if (string.IsNullOrWhiteSpace(_webhookSecret))
      {
          _logger.LogWarning(
              "MercadoPago:WebhookSecret não configurado. " +
              "Webhook validation BYPASSED — aceitando requisição sem assinatura. " +
              "Risco de segurança! Configure MercadoPago__WebhookSecret em produção.");
          return true;  // ✅ AGORA: permite passar em dev, avisa em prod
      }
  ```

- [ ] **3.2 — Adicionar log extra em produção**
  ```csharp
      if (string.IsNullOrWhiteSpace(_webhookSecret))
      {
          _logger.LogWarning(
              "MercadoPago:WebhookSecret não configurado. " +
              "Webhook validation BYPASSED — aceitando requisição sem assinatura. " +
              "Risco de segurança! Configure MercadoPago__WebhookSecret em produção.");
          
          // Se for Produção, loga com Critical em vez de Warning
          if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
          {
              _logger.LogCritical(
                  "🚨 PRODUÇÃO: Webhook validation DESABILITADA! " +
                  "Pagamentos podem ser falsificados. Configure MercadoPago__WebhookSecret IMEDIATAMENTE.");
          }
          
          return true;
      }
  ```

---

## 🏷️ Tópico 4 — Enviar Email em Background Job

**Gravidade:** 🟠 Alto | **Esforço:** ⭐⭐⭐ Médio-Alto (3-4h)

### Contexto
`ReservaService.ComprarIngressoAsync()` e `CancelarIngressoAsync()` enviam emails **sincronamente**. Se o SMTP estiver lento, o usuário espera. Precisamos de um `IHostedService` com fila em memória (`System.Threading.Channels`).

**Arquivos envolvidos:**
- `src/Service/ReservaService.cs` (2 chamadas de email)
- `src/Service/EventoService.cs` (notificação de cancelamento em lote)
- `src/Service/FilaEsperaService.cs` (notificação de vaga)
- NOVO: `src/Infrastructure/BackgroundEmailService.cs`
- NOVO: `src/Infrastructure/EmailJobItem.cs`

### Tarefas

- [ ] **4.1 — Criar modelo do job de email**
  **Arquivo:** `src/Infrastructure/EmailJobItem.cs`
  ```csharp
  namespace src.Infrastructure;
  
  public enum EmailJobType
  {
      PurchaseConfirmation,
      CancellationConfirmation,
      EventCancellationNotification,
      WaitingQueueNotification,
      EmailVerification,
      PasswordRecovery
  }
  
  public class EmailJobItem
  {
      public EmailJobType Type { get; init; }
      public string To { get; init; } = string.Empty;
      public string Subject { get; init; } = string.Empty;
      public string Body { get; init; } = string.Empty;
      public Guid JobId { get; } = Guid.NewGuid();
      public DateTime CreatedAt { get; } = DateTime.UtcNow;
  }
  ```

- [ ] **4.2 — Criar BackgroundEmailService**
  **Arquivo:** `src/Infrastructure/BackgroundEmailService.cs`
  ```csharp
  using System.Threading.Channels;
  
  namespace src.Infrastructure;
  
  /// <summary>
  /// Serviço em background que processa envios de email de forma assíncrona
  /// usando System.Threading.Channels (fila em memória, sem dependência externa).
  /// 
  /// Vantagens sobre o modelo síncrono atual:
  /// - HTTP response não espera o SMTP
  /// - Múltiplos emails podem ser processados em paralelo (até 3 simultâneos)
  /// - Graceful shutdown: espera fila esvaziar antes de parar
  /// - Falha de SMTP não quebra a requisição do usuário
  /// </summary>
  public sealed class BackgroundEmailService : IHostedService, IDisposable
  {
      private readonly Channel<EmailJobItem> _channel;
      private readonly IServiceScopeFactory _scopeFactory;
      private readonly ILogger<BackgroundEmailService> _logger;
      private readonly List<Task> _workers = new();
      private CancellationTokenSource? _cts;
  
      // Número de workers concorrentes
      private const int MaxConcurrentEmails = 3;
  
      public BackgroundEmailService(
          IServiceScopeFactory scopeFactory,
          ILogger<BackgroundEmailService> logger)
      {
          // Channel com backpressure: até 1000 emails na fila
          _channel = Channel.CreateBounded<EmailJobItem>(
              new BoundedChannelOptions(1000)
              {
                  FullMode = BoundedChannelFullMode.DropOldest
              });
          _scopeFactory = scopeFactory;
          _logger = logger;
      }
  
      /// <summary>
      /// Enfileira um email para envio em background.
      /// Retorna imediatamente — o email será enviado por um worker.
      /// </summary>
      public async Task EnqueueAsync(EmailJobItem job)
      {
          await _channel.Writer.WriteAsync(job);
          _logger.LogDebug(
              "Email enfileirado: {JobType} para {To} (JobId={JobId})",
              job.Type, job.To, job.JobId);
      }
  
      public Task StartAsync(CancellationToken cancellationToken)
      {
          _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
          
          for (int i = 0; i < MaxConcurrentEmails; i++)
          {
              var workerId = i + 1;
              _workers.Add(ProcessEmailsAsync(workerId, _cts.Token));
          }
          
          _logger.LogInformation(
              "BackgroundEmailService iniciado com {Count} workers",
              MaxConcurrentEmails);
          
          return Task.CompletedTask;
      }
  
      private async Task ProcessEmailsAsync(int workerId, CancellationToken ct)
      {
          await foreach (var job in _channel.Reader.ReadAllAsync(ct))
          {
              try
              {
                  // Cria um scope para resolver serviços scoped (IEmailService)
                  using var scope = _scopeFactory.CreateScope();
                  var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                  
                  _logger.LogInformation(
                      "[Worker {WorkerId}] Enviando email {JobType} para {To} (JobId={JobId})",
                      workerId, job.Type, job.To, job.JobId);
                  
                  await emailService.SendAsync(job.To, job.Subject, job.Body);
                  
                  _logger.LogInformation(
                      "[Worker {WorkerId}] Email enviado: {JobType} para {To}",
                      workerId, job.Type, job.To);
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex,
                      "[Worker {WorkerId}] Falha ao enviar email {JobType} para {To} (JobId={JobId})",
                      workerId, job.Type, job.To, job.JobId);
              }
          }
      }
  
      public async Task StopAsync(CancellationToken cancellationToken)
      {
          _logger.LogInformation("BackgroundEmailService parando...");
          _cts?.Cancel();
          
          // Aguarda workers terminarem os emails em andamento (máx 30s)
          await Task.WhenAll(_workers).WaitAsync(TimeSpan.FromSeconds(30));
          
          _logger.LogInformation("BackgroundEmailService parou");
      }
  
      public void Dispose()
      {
          _cts?.Dispose();
      }
  }
  ```

- [ ] **4.3 — Registrar no DI**
  **Arquivo:** `src/Program.cs`
  ```csharp
  // ── Background Email Queue ───────────────────────────────────────
  // Processa emails em background para não travar requisições HTTP.
  builder.Services.AddHostedService<BackgroundEmailService>();
  ```

- [ ] **4.4 — Modificar EmailTemplateService para aceitar enqueue**
  **Arquivo:** `src/Service/EmailTemplateService.cs`
  
  Adicionar dependência de `BackgroundEmailService`:
  ```csharp
  private readonly BackgroundEmailService _backgroundEmail;
  
  public EmailTemplateService(
      IEmailService emailService,
      BackgroundEmailService backgroundEmail,
      ILogger<EmailTemplateService> logger)
  {
      _emailService = emailService;
      _backgroundEmail = backgroundEmail;
      _logger = logger;
  }
  ```

  Depois, em cada método (ex: `SendPurchaseConfirmationAsync`), trocar de enviar direto para enfileirar:
  ```csharp
  // ANTES (síncrono):
  await _emailService.SendAsync(to, subject, body);
  
  // DEPOIS (background):
  await _backgroundEmail.EnqueueAsync(new EmailJobItem
  {
      Type = EmailJobType.PurchaseConfirmation,
      To = to,
      Subject = subject,
      Body = body
  });
  ```

  Repetir para:
  - `SendEmailVerificationAsync` → `EmailJobType.EmailVerification`
  - `SendPasswordRecoveryAsync` → `EmailJobType.PasswordRecovery`
  - `SendPurchaseConfirmationAsync` → `EmailJobType.PurchaseConfirmation`
  - `SendCancellationConfirmationAsync` → `EmailJobType.CancellationConfirmation`
  - `SendEventCancellationNotificationAsync` → `EmailJobType.EventCancellationNotification`
  - `SendWaitingQueueNotificationAsync` → `EmailJobType.WaitingQueueNotification`

- [ ] **4.5 — (Opcional) Método de extensão para enviar direto em produção**
  Se quiser manter a opção de enviar síncrono em cenários críticos:
  ```csharp
  public async Task SendDirectAsync(string to, string subject, string body)
  {
      await _emailService.SendAsync(to, subject, body);
  }
  ```

---

## 📊 Estimativa de Esforço Total

| Tópico | Arquivos | Tarefas | Tempo estimado |
|--------|----------|---------|----------------|
| 1 — Rotacionar SA + user-secrets | 2 | 6 | ~30 min |
| 2 — Criptografar ChavePix | 4 | 5 | ~2-3h |
| 3 — Webhook warning | 1 | 2 | ~15 min |
| 4 — Background email | 4 (2 novos) | 5 | ~3-4h |
| **Total** | **~11 arquivos** | **18 tarefas** | **~6-8h** |

---

## 🚀 Ordem Recomendada

```
1º → Tópico 3 (Webhook) — 15 min, mais fácil, remove risco de dev não perceber
2º → Tópico 1 (SA password) — 30 min, segurança crítica
3º → Tópico 2 (ChavePix) — 2-3h, requer mais cuidado
4º → Tópico 4 (Background email) — 3-4h, maior impacto em performance
```

Quer começar por algum deles? Posso implementar o **Tópico 3 (Webhook)** agora em 2 minutos — é o mais rápido.
