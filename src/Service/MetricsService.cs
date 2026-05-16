using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.RateLimiting;
using src.Infrastructure;

namespace src.Service;

/// <summary>
/// Serviço singleton que coleta métricas reais da aplicação para exposição
/// no formato Prometheus via endpoint GET /metrics.
///
/// ═══════════════════════════════════════════════════════════════════
/// AGORA: Também coleta métricas de Redis e integra com OpenTelemetry.
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public class MetricsService
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly RedisHealthCheck? _redisHealth;
    private readonly ILogger<MetricsService> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;

    // Contagem de vendas (ingressos)
    private long _totalSales;
    private long _totalRevenueCents; // Receita em centavos (int64) para evitar Interlocked com decimal

    // Contagem de usuários cadastrados (atualizado periodicamente)
    private long _totalUsers;

    // Último erro conhecido
    private string? _lastError;
    private DateTime _lastErrorTime;

    // Contador de requisições por endpoint
    private readonly ConcurrentDictionary<string, long> _requestCounts = new();

    // Duração total das requisições (para calcular média)
    private long _totalDurationTicks;

    // Total de requisições processadas
    private long _totalRequests;

    // Buckets do histograma de duração (em segundos)
    private readonly double[] _buckets = [0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0];
    private readonly ConcurrentDictionary<string, long> _histogram = new();

    // Contagem de erros HTTP por status code
    private readonly ConcurrentDictionary<int, long> _errorCounts = new();

    // Último resultado do health check de banco
    private bool _databaseUp;
    private DateTime _lastDbCheck = DateTime.MinValue;
    private readonly TimeSpan _dbCheckInterval = TimeSpan.FromSeconds(30);
    private readonly object _dbLock = new();

    public MetricsService(
        DbConnectionFactory dbFactory,
        ILogger<MetricsService> logger,
        RedisHealthCheck? redisHealth = null)
    {
        _dbFactory = dbFactory;
        _redisHealth = redisHealth;
        _logger = logger;

        // Inicializa buckets do histograma
        foreach (var bucket in _buckets)
        {
            _histogram.TryAdd($"le_{bucket:F2}", 0);
        }
        _histogram.TryAdd("le_+Inf", 0);
    }

    /// <summary>
    /// Registra uma requisição concluída.
    /// </summary>
    public void RecordRequest(string endpoint, int statusCode, long durationTicks)
    {
        var durationMs = TimeSpan.FromTicks(durationTicks).TotalSeconds;

        // Incrementa contador do endpoint
        _requestCounts.AddOrUpdate(endpoint, 1, (_, count) => count + 1);

        // Atualiza duração total
        Interlocked.Add(ref _totalDurationTicks, durationTicks);
        Interlocked.Increment(ref _totalRequests);

        // Atualiza histograma
        foreach (var bucket in _buckets)
        {
            if (durationMs <= bucket)
            {
                _histogram.AddOrUpdate($"le_{bucket:F2}", 1, (_, count) => count + 1);
            }
        }
        _histogram.AddOrUpdate("le_+Inf", 1, (_, count) => count + 1);

        // Contagem de erros (status >= 400)
        if (statusCode >= 400)
        {
            _errorCounts.AddOrUpdate(statusCode, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// Verifica se o banco de dados está acessível (com cache).
    /// </summary>
    public bool IsDatabaseUp()
    {
        // Cache do resultado por 30 segundos
        if ((DateTime.UtcNow - _lastDbCheck) < _dbCheckInterval)
            return _databaseUp;

        lock (_dbLock)
        {
            // Double-check após o lock
            if ((DateTime.UtcNow - _lastDbCheck) < _dbCheckInterval)
                return _databaseUp;

            try
            {
                using var conn = _dbFactory.CreateConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.ExecuteScalar();
                conn.Close();

                _databaseUp = true;
                _logger.LogDebug("Database health check: OK");
            }
            catch (Exception ex)
            {
                _databaseUp = false;
                _logger.LogWarning(ex, "Database health check: FAILED");
            }

            _lastDbCheck = DateTime.UtcNow;
            return _databaseUp;
        }
    }

    /// <summary>
    /// Gera o texto no formato Prometheus (text/plain; version=0.0.4).
    /// </summary>
    public string GenerateMetricsText(string environmentName)
    {
        var lines = new List<string>();
        var now = DateTime.UtcNow;

        // ─── ticketprime_up ──────────────────────────────────────────────
        lines.Add("# HELP ticketprime_up 1 if the API is running");
        lines.Add("# TYPE ticketprime_up gauge");
        lines.Add("ticketprime_up 1");
        lines.Add("");

        // ─── ticketprime_build_info ──────────────────────────────────────
        lines.Add("# HELP ticketprime_build_info Build information");
        lines.Add("# TYPE ticketprime_build_info gauge");
        lines.Add($"ticketprime_build_info{{version=\"1.0\",environment=\"{environmentName}\"}} 1");
        lines.Add("");

        // ─── ticketprime_database_up ─────────────────────────────────────
        lines.Add("# HELP ticketprime_database_up 1 if database is accessible");
        lines.Add("# TYPE ticketprime_database_up gauge");
        lines.Add($"ticketprime_database_up {(IsDatabaseUp() ? 1 : 0)}");
        lines.Add("");

        // ─── ticketprime_cache_up ────────────────────────────────────────
        lines.Add("# HELP ticketprime_cache_up 1 if Redis/distributed cache is accessible");
        lines.Add("# TYPE ticketprime_cache_up gauge");
        var cacheUp = _redisHealth?.IsAvailable ?? false;
        lines.Add($"ticketprime_cache_up {(cacheUp ? 1 : 0)}");
        lines.Add("");

        // ─── ticketprime_cache_type ──────────────────────────────────────
        lines.Add("# HELP ticketprime_cache_type Type of cache in use (redis or memory)");
        lines.Add("# TYPE ticketprime_cache_type gauge");
        var cacheType = _redisHealth != null ? "redis" : "memory";
        lines.Add($"ticketprime_cache_type{{type=\"{cacheType}\"}} 1");
        lines.Add("");

        // ─── ticketprime_total_sales ─────────────────────────────────────
        lines.Add("# HELP ticketprime_total_sales Total number of tickets sold");
        lines.Add("# TYPE ticketprime_total_sales counter");
        lines.Add($"ticketprime_total_sales {Interlocked.Read(ref _totalSales)}");
        lines.Add("");

        // ─── ticketprime_total_revenue ───────────────────────────────────
        lines.Add("# HELP ticketprime_total_revenue Total revenue from ticket sales");
        lines.Add("# TYPE ticketprime_total_revenue counter");
        var totalRevenue = Interlocked.Read(ref _totalRevenueCents) / 100m;
        lines.Add($"ticketprime_total_revenue {totalRevenue:F2}");
        lines.Add("");

        // ─── ticketprime_total_users ────────────────────────────────────
        lines.Add("# HELP ticketprime_total_users Total registered users");
        lines.Add("# TYPE ticketprime_total_users gauge");
        lines.Add($"ticketprime_total_users {Interlocked.Read(ref _totalUsers)}");
        lines.Add("");

        // ─── ticketprime_last_error ──────────────────────────────────────
        if (_lastError != null)
        {
            lines.Add("# HELP ticketprime_last_error Timestamp and message of last error");
            lines.Add("# TYPE ticketprime_last_error gauge");
            lines.Add($"ticketprime_last_error{{error=\"{_lastError}\"}} {new DateTimeOffset(_lastErrorTime).ToUnixTimeSeconds()}");
            lines.Add("");
        }

        // ─── ticketprime_requests_total ──────────────────────────────────
        lines.Add("# HELP ticketprime_requests_total Total requests by endpoint");
        lines.Add("# TYPE ticketprime_requests_total counter");
        foreach (var kvp in _requestCounts)
        {
            lines.Add($"ticketprime_requests_total{{endpoint=\"{kvp.Key}\"}} {kvp.Value}");
        }
        // Total geral
        var total = Interlocked.Read(ref _totalRequests);
        lines.Add($"ticketprime_requests_total{{endpoint=\"all\"}} {total}");
        lines.Add("");

        // ─── ticketprime_request_duration_seconds (histogram) ────────────
        lines.Add("# HELP ticketprime_request_duration_seconds Request duration histogram");
        lines.Add("# TYPE ticketprime_request_duration_seconds histogram");
        foreach (var bucket in _buckets)
        {
            var count = _histogram.GetValueOrDefault($"le_{bucket:F2}", 0);
            lines.Add($"ticketprime_request_duration_seconds_bucket{{le=\"{bucket:F2}\"}} {count}");
        }
        var infCount = _histogram.GetValueOrDefault("le_+Inf", 0);
        lines.Add($"ticketprime_request_duration_seconds_bucket{{le=\"+Inf\"}} {infCount}");
        lines.Add($"ticketprime_request_duration_seconds_count {total}");

        // Soma das durações
        var totalSeconds = TimeSpan.FromTicks(Interlocked.Read(ref _totalDurationTicks)).TotalSeconds;
        lines.Add($"ticketprime_request_duration_seconds_sum {totalSeconds:F6}");
        lines.Add("");

        // ─── ticketprime_http_errors_total ───────────────────────────────
        lines.Add("# HELP ticketprime_http_errors_total HTTP errors by status code");
        lines.Add("# TYPE ticketprime_http_errors_total counter");
        foreach (var kvp in _errorCounts)
        {
            lines.Add($"ticketprime_http_errors_total{{status=\"{kvp.Key}\"}} {kvp.Value}");
        }
        lines.Add("");

        // ─── ticketprime_uptime_seconds ──────────────────────────────────
        lines.Add("# HELP ticketprime_uptime_seconds Uptime in seconds");
        lines.Add("# TYPE ticketprime_uptime_seconds counter");
        var uptime = (now - _startTime).TotalSeconds;
        lines.Add($"ticketprime_uptime_seconds {uptime:F0}");
        lines.Add("");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Registra uma venda de ingresso (para métrica de vendas).
    /// </summary>
    public void RecordSale(decimal valor)
    {
        Interlocked.Increment(ref _totalSales);
        Interlocked.Add(ref _totalRevenueCents, (long)(valor * 100));
    }

    /// <summary>
    /// Atualiza o contador de usuários cadastrados.
    /// </summary>
    public void UpdateUserCount(long count)
    {
        Interlocked.Exchange(ref _totalUsers, count);
    }

    /// <summary>
    /// Registra um erro para exposição nas métricas.
    /// </summary>
    public void RecordError(string errorMessage)
    {
        _lastError = errorMessage;
        _lastErrorTime = DateTime.UtcNow;
    }
}
