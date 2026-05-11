using System.Text.Json;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Service;

/// <summary>
/// Serviço de auditoria financeira estruturada.
/// 
/// Cada transação financeira sensível (compra, cancelamento, login, cadastro)
/// é registrada com trilha imutável: quem, qual IP, qual horário, qual valor exato,
/// com encadeamento hash SHA256 para garantir integridade.
/// 
/// Uso típico em controllers:
///   var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
///   var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();
///   await _auditLog.LogCompraIngressoAsync(cpf, eventoId, reservaId, valorFinal, ip, userAgent, detalhesJson);
/// </summary>
public class AuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository repository, ILogger<AuditLogService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Registra a compra de um ingresso (principal transação financeira).
    /// </summary>
    public async Task LogCompraIngressoAsync(
        string usuarioCpf,
        int eventoId,
        int reservaId,
        decimal valorFinalPago,
        decimal taxaServico,
        decimal valorSeguro,
        string? cupomUtilizado,
        decimal? valorDesconto,
        string ipAddress,
        string? userAgent)
    {
        var detalhes = new
        {
            taxaServico,
            valorSeguro,
            cupomUtilizado,
            valorDesconto,
            ingressoGratuito = valorFinalPago == 0
        };

        var entry = new AuditLogEntry
        {
            ActionType = "CompraIngresso",
            UsuarioCpf = usuarioCpf,
            EventoId = eventoId,
            ReservaId = reservaId,
            ValorTransacionado = valorFinalPago,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Detalhes = JsonSerializer.Serialize(detalhes)
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra o cancelamento de um ingresso (com valor de devolução).
    /// </summary>
    public async Task LogCancelamentoIngressoAsync(
        string usuarioCpf,
        int eventoId,
        int reservaId,
        decimal valorDevolvido,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "CancelamentoIngresso",
            UsuarioCpf = usuarioCpf,
            EventoId = eventoId,
            ReservaId = reservaId,
            ValorTransacionado = valorDevolvido,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra um login bem-sucedido.
    /// </summary>
    public async Task LogLoginAsync(
        string usuarioCpf,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "Login",
            UsuarioCpf = usuarioCpf,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra uma tentativa de login mal-sucedida.
    /// </summary>
    public async Task LogLoginFalhaAsync(
        string cpfTentativa,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "LoginFalha",
            UsuarioCpf = cpfTentativa,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra o cadastro de um novo usuário.
    /// </summary>
    public async Task LogCadastroUsuarioAsync(
        string usuarioCpf,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "CadastroUsuario",
            UsuarioCpf = usuarioCpf,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra a troca de senha.
    /// </summary>
    public async Task LogTrocaSenhaAsync(
        string usuarioCpf,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "TrocaSenha",
            UsuarioCpf = usuarioCpf,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra a redefinição de senha (via token de recuperação).
    /// </summary>
    public async Task LogRedefinicaoSenhaAsync(
        string usuarioCpf,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "RedefinicaoSenha",
            UsuarioCpf = usuarioCpf,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra um refresh de token.
    /// </summary>
    public async Task LogRefreshTokenAsync(
        string usuarioCpf,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "RefreshToken",
            UsuarioCpf = usuarioCpf,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Registra o check-in físico de um ingresso na entrada do evento.
    /// </summary>
    public async Task LogCheckinIngressoAsync(
        string usuarioCpf,
        int eventoId,
        int reservaId,
        string ipAddress,
        string? userAgent)
    {
        var entry = new AuditLogEntry
        {
            ActionType = "CheckinIngresso",
            UsuarioCpf = usuarioCpf,
            EventoId = eventoId,
            ReservaId = reservaId,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Método genérico para registrar entradas de auditoria customizadas.
    /// </summary>
    public async Task LogCustomAsync(
        string actionType,
        string? usuarioCpf,
        int? eventoId,
        int? reservaId,
        decimal? valorTransacionado,
        string ipAddress,
        string? userAgent,
        object? detalhes = null)
    {
        var entry = new AuditLogEntry
        {
            ActionType = actionType,
            UsuarioCpf = usuarioCpf,
            EventoId = eventoId,
            ReservaId = reservaId,
            ValorTransacionado = valorTransacionado,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Detalhes = detalhes != null ? JsonSerializer.Serialize(detalhes) : null
        };

        await LogAsync(entry);
    }

    /// <summary>
    /// Verifica a integridade de toda a cadeia de auditoria.
    /// Útil para relatórios de compliance e auditoria externa.
    /// </summary>
    public async Task<(bool Integra, long TotalRegistros)> VerificarIntegridadeAsync()
    {
        var integra = await _repository.VerificarIntegridadeAsync();
        var total = await _repository.ContarTotalAsync();
        return (integra, total);
    }

    /// <summary>
    /// Obtém todas as entradas de auditoria de um usuário.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> ListarPorUsuarioAsync(string cpf)
    {
        return await _repository.ListarPorUsuarioAsync(cpf);
    }

    /// <summary>
    /// Obtém entradas de auditoria por tipo de ação.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> ListarPorTipoAcaoAsync(string actionType, int limite = 100)
    {
        return await _repository.ListarPorTipoAcaoAsync(actionType, limite);
    }

    /// <summary>
    /// Obtém entradas de auditoria em um período.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> ListarPorPeriodoAsync(DateTime inicio, DateTime fim)
    {
        return await _repository.ListarPorPeriodoAsync(inicio, fim);
    }

    // ── Método privado para persistir com tratamento de erro ─────────────

    private async Task LogAsync(AuditLogEntry entry)
    {
        try
        {
            await _repository.InserirAsync(entry);
            _logger.LogDebug(
                "AuditLog [{ActionType}] CPF={UsuarioCpf} IP={IpAddress} Valor={Valor}",
                entry.ActionType, entry.UsuarioCpf, entry.IpAddress, entry.ValorTransacionado);
        }
        catch (Exception ex)
        {
            // Falha no audit log não deve nunca quebrar a operação principal
            _logger.LogError(ex,
                "Falha ao registrar auditoria [{ActionType}] para CPF={UsuarioCpf}",
                entry.ActionType, entry.UsuarioCpf);
        }
    }
}
