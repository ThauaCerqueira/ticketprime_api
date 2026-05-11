using Dapper;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public sealed class MeiaEntradaRepository : IMeiaEntradaRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public MeiaEntradaRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InserirAsync(MeiaEntradaDocumento documento)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO MeiaEntradaDocumentos (
                ReservaId, UsuarioCpf, EventoId,
                CaminhoArquivo, NomeOriginal, TipoMime, TamanhoBytes,
                Status, DataUpload)
            VALUES (
                @ReservaId, @UsuarioCpf, @EventoId,
                @CaminhoArquivo, @NomeOriginal, @TipoMime, @TamanhoBytes,
                @Status, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT)";

        var id = await connection.QuerySingleAsync<int>(sql, documento);
        return id;
    }

    public async Task VincularReservaAsync(int documentoId, int reservaId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE MeiaEntradaDocumentos SET ReservaId = @ReservaId WHERE Id = @Id",
            new { Id = documentoId, ReservaId = reservaId });
    }

    public async Task<MeiaEntradaDocumento?> ObterPorIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<MeiaEntradaDocumento>(
            "SELECT * FROM MeiaEntradaDocumentos WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<List<MeiaEntradaDocumentoDto>> ListarPendentesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT
                d.Id,
                d.ReservaId,
                d.UsuarioCpf,
                u.Nome AS UsuarioNome,
                d.EventoId,
                e.Nome AS EventoNome,
                d.NomeOriginal,
                d.TipoMime,
                d.TamanhoBytes,
                d.Status,
                d.DataUpload,
                d.DataVerificacao,
                d.VerificadoPorCpf,
                d.MotivoRejeicao
            FROM MeiaEntradaDocumentos d
            INNER JOIN Usuarios u ON u.Cpf = d.UsuarioCpf
            INNER JOIN Eventos e ON e.Id = d.EventoId
            WHERE d.Status = 'Pendente'
            ORDER BY d.DataUpload ASC";

        var result = await connection.QueryAsync<MeiaEntradaDocumentoDto>(sql);
        return result.AsList();
    }

    public async Task<List<MeiaEntradaDocumentoDto>> ListarTodosAsync(string? filtroStatus = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT
                d.Id,
                d.ReservaId,
                d.UsuarioCpf,
                u.Nome AS UsuarioNome,
                d.EventoId,
                e.Nome AS EventoNome,
                d.NomeOriginal,
                d.TipoMime,
                d.TamanhoBytes,
                d.Status,
                d.DataUpload,
                d.DataVerificacao,
                d.VerificadoPorCpf,
                d.MotivoRejeicao
            FROM MeiaEntradaDocumentos d
            INNER JOIN Usuarios u ON u.Cpf = d.UsuarioCpf
            INNER JOIN Eventos e ON e.Id = d.EventoId
            WHERE 1=1";

        if (!string.IsNullOrEmpty(filtroStatus))
            sql += " AND d.Status = @Status";

        sql += " ORDER BY d.DataUpload DESC";

        var result = await connection.QueryAsync<MeiaEntradaDocumentoDto>(
            sql, new { Status = filtroStatus });
        return result.AsList();
    }

    public async Task AtualizarStatusAsync(int id, string status, string verificadoPorCpf, string? motivoRejeicao = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE MeiaEntradaDocumentos
            SET Status = @Status,
                DataVerificacao = GETUTCDATE(),
                VerificadoPorCpf = @VerificadoPorCpf,
                MotivoRejeicao = @MotivoRejeicao
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            Status = status,
            VerificadoPorCpf = verificadoPorCpf,
            MotivoRejeicao = motivoRejeicao
        });
    }

    public async Task<int> ContarPendentesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM MeiaEntradaDocumentos WHERE Status = 'Pendente'");
    }

    public async Task<MeiaEntradaDocumentoDto?> ObterPorReservaIdAsync(int reservaId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT
                d.Id,
                d.ReservaId,
                d.UsuarioCpf,
                u.Nome AS UsuarioNome,
                d.EventoId,
                e.Nome AS EventoNome,
                d.NomeOriginal,
                d.TipoMime,
                d.TamanhoBytes,
                d.Status,
                d.DataUpload,
                d.DataVerificacao,
                d.VerificadoPorCpf,
                d.MotivoRejeicao
            FROM MeiaEntradaDocumentos d
            INNER JOIN Usuarios u ON u.Cpf = d.UsuarioCpf
            INNER JOIN Eventos e ON e.Id = d.EventoId
            WHERE d.ReservaId = @ReservaId";

        return await connection.QuerySingleOrDefaultAsync<MeiaEntradaDocumentoDto>(
            sql, new { ReservaId = reservaId });
    }
}
