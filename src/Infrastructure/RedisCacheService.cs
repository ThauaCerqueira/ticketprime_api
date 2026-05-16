using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace src.Infrastructure;

/// <summary>
/// Serviço de cache distribuído via Redis (ou memória como fallback).
///
/// ═══════════════════════════════════════════════════════════════════
/// ANTES: IMemoryCache (single-instance).
///   Cada réplica da API tinha seu próprio cache — dados inconsistentes
///   quando escalado horizontalmente.
///
/// AGORA: IDistributedCache com Redis.
///   Cache compartilhado entre todas as réplicas. Dados consistentes
///   independentemente de quantas instâncias da API estejam rodando.
///   Fallback para MemoryCache quando Redis não está configurado.
/// ═══════════════════════════════════════════════════════════════════
///
/// Uso:
///   builder.Services.AddStackExchangeRedisCache(options => {
///       options.Configuration = "redis:6379";
///   });
///   // ou sem Redis (fallback):
///   builder.Services.AddDistributedMemoryCache();
/// </summary>
public static class RedisCacheExtensions
{
    /// <summary>
    /// Configura o cache distribuído: Redis se configurado, senão MemoryCache.
    /// </summary>
    public static IServiceCollection AddTicketPrimeCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration["Redis:Connection"]
            ?? configuration["Redis__Connection"];

        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "TicketPrime:";

                // Timeout de conexão: 5s para não travar a API se Redis cair
                options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
                {
                    ConnectTimeout = 5000,
                    SyncTimeout = 3000,
                    AbortOnConnectFail = false, // Não derruba a API se Redis estiver offline
                    EndPoints = { redisConnection }
                };
            });

            // Registra health checker para Redis
            services.AddSingleton<RedisHealthCheck>();
        }
        else
        {
            // Fallback: cache em memória (single-instance)
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}

/// <summary>
/// Health checker para Redis — verifica conectividade com o servidor Redis.
/// </summary>
public sealed class RedisHealthCheck
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisHealthCheck> _logger;
    private bool _lastStatus;
    private DateTime _lastCheck = DateTime.MinValue;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public RedisHealthCheck(IDistributedCache cache, ILogger<RedisHealthCheck> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Verifica se o Redis está acessível (com cache de 30s).
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            if ((DateTime.UtcNow - _lastCheck) < _checkInterval)
                return _lastStatus;

            try
            {
                // Tenta escrever e ler uma chave de teste
                var testKey = $"health-check-{Environment.MachineName}";
                var testValue = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                _cache.Set(testKey, testValue, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                });
                var result = _cache.Get(testKey);

                _lastStatus = result != null;
                if (!_lastStatus)
                    _logger.LogWarning("Redis health check: FAILED (read returned null)");
            }
            catch (Exception ex)
            {
                _lastStatus = false;
                _logger.LogWarning(ex, "Redis health check: FAILED (exception)");
            }

            _lastCheck = DateTime.UtcNow;
            return _lastStatus;
        }
    }
}

/// <summary>
/// Extensões para IDistributedCache com serialização JSON.
/// </summary>
public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Obtém um valor do cache, desserializando do JSON.
    /// </summary>
    public static async Task<T?> GetAsync<T>(this IDistributedCache cache, string key)
        where T : class
    {
        var bytes = await cache.GetAsync(key);
        if (bytes == null) return null;

        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<T>(json, _jsonOpts);
    }

    /// <summary>
    /// Armazena um valor no cache, serializando para JSON.
    /// </summary>
    public static async Task SetAsync<T>(
        this IDistributedCache cache,
        string key,
        T value,
        TimeSpan? expiration = null)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, _jsonOpts);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiration;

        await cache.SetAsync(key, bytes, options);
    }
}
