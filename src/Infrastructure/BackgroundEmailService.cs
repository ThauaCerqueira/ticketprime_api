using System.Threading.Channels;
using src.Service;

namespace src.Infrastructure;

/// <summary>
/// Serviço em background que processa envios de email de forma assíncrona
/// usando System.Threading.Channels (fila em memória, sem dependência externa).
/// 
/// ═══════════════════════════════════════════════════════════════════
/// ANTES (síncrono):
///   ReservaService → EmailTemplateService → SmtpClient.SendAsync()
///   O usuário esperava o SMTP responder para receber o HTTP 201.
/// 
/// AGORA (assíncrono):
///   ReservaService → EmailTemplateService → Channel.WriteAsync()
///   HTTP 201 retorna IMEDIATAMENTE. O email é enviado por um worker.
/// ═══════════════════════════════════════════════════════════════════
/// 
/// Vantagens:
/// - HTTP response não espera o SMTP
/// - 3 workers concorrentes processam emails em paralelo
/// - Graceful shutdown com drain da fila
/// - Falha de SMTP não quebra a requisição do usuário
/// </summary>
public sealed class BackgroundEmailService : IHostedService, IDisposable
{
    private readonly Channel<EmailJobItem> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundEmailService> _logger;
    private readonly List<Task> _workers = [];
    private CancellationTokenSource? _cts;

    /// <summary>Número de workers concorrentes para envio de email.</summary>
    private const int MaxConcurrentWorkers = 3;

    /// <summary>Capacidade máxima da fila (backpressure).</summary>
    private const int MaxQueueSize = 1000;

    public BackgroundEmailService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _channel = Channel.CreateBounded<EmailJobItem>(
            new BoundedChannelOptions(MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                Capacity = MaxQueueSize
            });
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

        for (int i = 0; i < MaxConcurrentWorkers; i++)
        {
            var workerId = i + 1;
            _workers.Add(ProcessEmailsAsync(workerId, _cts.Token));
        }

        _logger.LogInformation(
            "BackgroundEmailService iniciado com {Count} workers",
            MaxConcurrentWorkers);

        return Task.CompletedTask;
    }

    private async Task ProcessEmailsAsync(int workerId, CancellationToken ct)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
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

        try
        {
            // Aguarda workers terminarem os emails em andamento (máx 30s)
            await Task.WhenAll(_workers).WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "BackgroundEmailService: timeout ao aguardar workers ({Count} ainda rodando)",
                _workers.Count(t => !t.IsCompleted));
        }

        _logger.LogInformation("BackgroundEmailService parou");
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
