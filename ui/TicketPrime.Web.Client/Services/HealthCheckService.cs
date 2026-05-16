using Microsoft.Extensions.Logging;

namespace TicketPrime.Web.Client.Services;

/// <summary>
/// Serviço que monitora periodicamente a saúde da API backend via endpoint /health.
/// Expõe o status atual para que a UI exiba um banner amigável quando o backend estiver offline.
/// </summary>
public sealed class HealthCheckService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly CancellationTokenSource _cts = new();

    private const int IntervaloSegundos = 30;

    /// <summary>
    /// Indica se o backend está operacional (última chamada a /health foi bem-sucedida).
    /// </summary>
    public bool IsApiHealthy { get; private set; } = true;

    /// <summary>
    /// Timestamp da última falha de conexão com a API.
    /// </summary>
    public DateTime? UltimaFalha { get; private set; }

    /// <summary>
    /// Disparado quando o status de saúde muda.
    /// </summary>
    public event Action<bool, string?>? OnStatusChanged;

    public HealthCheckService(IHttpClientFactory httpClientFactory, ILogger<HealthCheckService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TicketPrimeApi");
        _logger = logger;
    }

    public void IniciarMonitoramento()
    {
        // Executa verificação inicial imediata
        _ = VerificarSaudeAsync();

        // Inicia monitoramento periódico em background
        _ = Task.Run(() => MonitorarAsync(_cts.Token));
    }

    public void PararMonitoramento()
    {
        _cts.Cancel();
    }

    private async Task MonitorarAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(IntervaloSegundos));

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await VerificarSaudeAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Monitoramento interrompido normalmente
        }
    }

    private async Task VerificarSaudeAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync("/health", cts.Token);

            var saudavel = response.IsSuccessStatusCode;
            var anterior = IsApiHealthy;
            IsApiHealthy = saudavel;

            if (!saudavel)
            {
                UltimaFalha = DateTime.UtcNow;
                _logger.LogWarning("HealthCheck: API retornou status {StatusCode}", response.StatusCode);
            }

            if (anterior != saudavel)
            {
                var mensagem = saudavel
                    ? "API reconectada com sucesso."
                    : "API indisponível — servidor pode estar offline.";
                _logger.LogInformation("HealthCheck: status mudou para {Status} — {Mensagem}", saudavel ? "OK" : "FALHA", mensagem);
                OnStatusChanged?.Invoke(saudavel, mensagem);
            }
        }
        catch (HttpRequestException ex)
        {
            var anterior = IsApiHealthy;
            IsApiHealthy = false;
            UltimaFalha = DateTime.UtcNow;

            _logger.LogWarning(ex, "HealthCheck: falha de conexão com a API");

            if (anterior)
            {
                OnStatusChanged?.Invoke(false, "Não foi possível conectar ao servidor. Verifique sua conexão de internet ou tente novamente mais tarde.");
            }
        }
        catch (TaskCanceledException)
        {
            var anterior = IsApiHealthy;
            IsApiHealthy = false;
            UltimaFalha = DateTime.UtcNow;

            _logger.LogWarning("HealthCheck: requisição excedeu o tempo limite (5s)");

            if (anterior)
            {
                OnStatusChanged?.Invoke(false, "O servidor está demorando para responder. Tente novamente mais tarde.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HealthCheck: erro inesperado");
            IsApiHealthy = false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
