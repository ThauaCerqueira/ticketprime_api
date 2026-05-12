using Dapper;
using src.Infrastructure;

namespace src.Service;

/// <summary>
/// Background service that runs once per day and purges expired or revoked
/// refresh tokens from the RefreshTokens table to prevent unbounded growth.
/// </summary>
public sealed class RefreshTokenCleanupService : BackgroundService
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public RefreshTokenCleanupService(
        DbConnectionFactory dbFactory,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Stagger the first run to avoid hitting the DB at startup under load.
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PurgeExpiredTokensAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — the host is stopping (e.g. due to port bind failure
            // or normal application termination). This is expected and should not
            // propagate as an unhandled exception.
            _logger.LogDebug("RefreshTokenCleanupService stopped gracefully.");
        }
    }

    private async Task PurgeExpiredTokensAsync(CancellationToken ct)
    {
        try
        {
            using var connection = _dbFactory.CreateConnection();
            var deleted = await connection.ExecuteAsync(
                @"DELETE FROM RefreshTokens
                  WHERE ExpiresAt < GETUTCDATE()
                     OR RevokedAt IS NOT NULL");

            if (deleted > 0)
                _logger.LogInformation(
                    "RefreshToken cleanup: {Count} stale token(s) removed.", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log and continue — a failed cleanup is non-critical and will
            // be retried on the next interval.
            _logger.LogError(ex, "RefreshToken cleanup failed.");
        }
    }
}
