using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Models;

namespace src.Infrastructure.Repository;

public class EventoRepository : IEventoRepository
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<EventoRepository> _logger;

    public EventoRepository(DbConnectionFactory connectionFactory, ILogger<EventoRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<int> AdicionarAsync(TicketEvent evento)
    {
        const string sql = @"
            INSERT INTO Eventos (
                Nome, CapacidadeTotal, CapacidadeRestante, DataEvento, DataTermino, PrecoPadrao, LimiteIngressosPorUsuario,
                Local, Descricao, GeneroMusical, EventoGratuito, Status, TaxaServico, TemMeiaEntrada, OrganizadorCpf,
                Categoria, ImagemUrl)
            OUTPUT INSERTED.Id
            VALUES (
                @Nome, @CapacidadeTotal, @CapacidadeTotal, @DataEvento, @DataTermino, @PrecoPadrao, @LimiteIngressosPorUsuario,
                @Local, @Descricao, @GeneroMusical, @EventoGratuito, @Status, @TaxaServico, @TemMeiaEntrada, @OrganizadorCpf,
                @Categoria, @ImagemUrl)";

        using var connection = _connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<int>(sql, evento);
        evento.Id = id;
        return id;
    }

    public async Task<PaginatedResult<TicketEvent>> ObterTodosAsync(int pagina = 1, int tamanhoPagina = 20)
    {
        using var conn = _connectionFactory.CreateConnection();
        int offset = (pagina - 1) * tamanhoPagina;

        var countSql = "SELECT COUNT(*) FROM Eventos";
        var total = await conn.QuerySingleAsync<int>(countSql);

        string sql = @"SELECT * FROM Eventos ORDER BY DataEvento ASC
                       OFFSET @Offset ROWS FETCH NEXT @TamanhoPagina ROWS ONLY";
        var itens = await conn.QueryAsync<TicketEvent>(sql, new { Offset = offset, TamanhoPagina = tamanhoPagina });

        return new PaginatedResult<TicketEvent>
        {
            Itens = itens,
            Total = total,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina
        };
    }

    public async Task<IEnumerable<TicketEvent>> ObterDisponiveisAsync()
    {
        using var conn = _connectionFactory.CreateConnection();
        string sql = @"
            SELECT e.*, ft.ThumbnailBase64 AS FotoThumbnailBase64
            FROM Eventos e
            LEFT JOIN (
                SELECT EventoId, ThumbnailBase64,
                       ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY Id) AS rn
                FROM EventoFotos
                WHERE ThumbnailBase64 IS NOT NULL
            ) ft ON ft.EventoId = e.Id AND ft.rn = 1
            WHERE e.DataEvento > GETDATE()
              AND e.CapacidadeRestante > 0
              AND e.Status = 'Publicado'
            ORDER BY e.DataEvento ASC";
        return await conn.QueryAsync<TicketEvent>(sql);
    }

    public async Task<PaginatedResult<TicketEvent>> BuscarDisponiveisAsync(
        string? nome, string? genero, DateTime? dataMin, DateTime? dataMax,
        string? cidade = null, int pagina = 1, int tamanhoPagina = 20)
    {
        using var conn = _connectionFactory.CreateConnection();
        int offset = (pagina - 1) * tamanhoPagina;

        if (!string.IsNullOrWhiteSpace(nome))
        {
            try
            {
                return await BuscarPorFullTextAsync(conn, nome, genero, dataMin, dataMax, cidade, offset, pagina, tamanhoPagina);
            }
            catch (Exception ex) when (ex.Message.Contains("Full-Text Search", StringComparison.OrdinalIgnoreCase)
                                       || ex.Message.Contains("full-text", StringComparison.OrdinalIgnoreCase)
                                       || ex.Message.Contains("FREETEXT", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex,
                    "Full-Text Search indisponível (SQL Server Express?). " +
                    "Fazendo fallback para busca LIKE. Considere usar Developer/Standard+ para full-text search.");

                return await BuscarPorLikeAsync(conn, nome, genero, dataMin, dataMax, cidade, offset, pagina, tamanhoPagina);
            }
        }

        // ── Sem termo de busca: ordenação por data (comportamento original) ──
        return await BuscarSemNomeAsync(conn, genero, dataMin, dataMax, cidade, offset, pagina, tamanhoPagina);
    }

    /// <summary>
    /// Busca com FREETEXTTABLE (full-text search c/ relevância).
    /// REQUER: SQL Server Standard/Developer/Enterprise.
    /// </summary>
    private async Task<PaginatedResult<TicketEvent>> BuscarPorFullTextAsync(
        IDbConnection conn, string nome, string? genero, DateTime? dataMin, DateTime? dataMax,
        string? cidade,
        int offset, int pagina, int tamanhoPagina)
    {
        var cidadeFiltro = string.IsNullOrWhiteSpace(cidade) ? "" : " AND e.Cidade LIKE @Cidade";

        var countSql = $@"
            SELECT COUNT(*)
            FROM Eventos e
            INNER JOIN FREETEXTTABLE(Eventos, (Nome, Descricao, Local, GeneroMusical), @Nome) ft
                ON e.Id = ft.[KEY]
            WHERE e.DataEvento > GETDATE()
              AND e.CapacidadeRestante > 0
              AND e.Status = 'Publicado'
              AND (@Genero IS NULL OR e.GeneroMusical = @Genero)
              AND (@DataMin IS NULL OR e.DataEvento >= @DataMin)
              AND (@DataMax IS NULL OR e.DataEvento <= @DataMax){cidadeFiltro}";

        var total = await conn.QuerySingleAsync<int>(countSql,
            new { Nome = nome, Genero = genero, DataMin = dataMin, DataMax = dataMax, Cidade = $"%{cidade}%" });

        var sql = $@"
            SELECT e.*, fotos.ThumbnailBase64 AS FotoThumbnailBase64,
                   COALESCE((SELECT AVG(CAST(a.Nota AS FLOAT)) FROM Avaliacoes a WHERE a.EventoId = e.Id), 0) AS NotaMedia
            FROM Eventos e
            LEFT JOIN (
                SELECT EventoId, ThumbnailBase64,
                       ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY Id) AS rn
                FROM EventoFotos
                WHERE ThumbnailBase64 IS NOT NULL
            ) fotos ON fotos.EventoId = e.Id AND fotos.rn = 1
            INNER JOIN FREETEXTTABLE(Eventos, (Nome, Descricao, Local, GeneroMusical), @Nome) ft
                ON e.Id = ft.[KEY]
            WHERE e.DataEvento > GETDATE()
              AND e.CapacidadeRestante > 0
              AND e.Status = 'Publicado'
              AND (@Genero IS NULL OR e.GeneroMusical = @Genero)
              AND (@DataMin IS NULL OR e.DataEvento >= @DataMin)
              AND (@DataMax IS NULL OR e.DataEvento <= @DataMax){cidadeFiltro}
            ORDER BY ft.RANK DESC
            OFFSET @Offset ROWS FETCH NEXT @TamanhoPagina ROWS ONLY";

        var itens = await conn.QueryAsync<TicketEvent>(sql,
            new { Nome = nome, Genero = genero, DataMin = dataMin, DataMax = dataMax,
                  Cidade = $"%{cidade}%",
                  Offset = offset, TamanhoPagina = tamanhoPagina });

        return new PaginatedResult<TicketEvent>
        {
            Itens = itens,
            Total = total,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina
        };
    }

    /// <summary>
    /// Fallback: busca com LIKE (quando full-text search não está disponível).
    /// Menos performática, mas compatível com SQL Server Express.
    /// </summary>
    private async Task<PaginatedResult<TicketEvent>> BuscarPorLikeAsync(
        IDbConnection conn, string nome, string? genero, DateTime? dataMin, DateTime? dataMax,
        string? cidade,
        int offset, int pagina, int tamanhoPagina)
    {
        var likePattern = $"%{nome}%";
        var cidadeCond = string.IsNullOrWhiteSpace(cidade) ? "" : " AND Cidade LIKE @Cidade";

        var baseWhere = $@"
            WHERE DataEvento > GETDATE()
              AND CapacidadeRestante > 0
              AND Status = 'Publicado'
              AND (@Genero IS NULL OR GeneroMusical = @Genero)
              AND (@DataMin IS NULL OR DataEvento >= @DataMin)
              AND (@DataMax IS NULL OR DataEvento <= @DataMax){cidadeCond}
              AND (Nome LIKE @Pattern OR Descricao LIKE @Pattern OR Local LIKE @Pattern OR GeneroMusical LIKE @Pattern)";

        var countSql = "SELECT COUNT(*) FROM Eventos " + baseWhere;
        var total = await conn.QuerySingleAsync<int>(countSql,
            new { Genero = genero, DataMin = dataMin, DataMax = dataMax, Pattern = likePattern, Cidade = $"%{cidade}%" });

        var sql = @"
            SELECT e.*, fotos.ThumbnailBase64 AS FotoThumbnailBase64,
                   COALESCE((SELECT AVG(CAST(a.Nota AS FLOAT)) FROM Avaliacoes a WHERE a.EventoId = e.Id), 0) AS NotaMedia
            FROM Eventos e
            LEFT JOIN (
                SELECT EventoId, ThumbnailBase64,
                       ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY Id) AS rn
                FROM EventoFotos
                WHERE ThumbnailBase64 IS NOT NULL
            ) fotos ON fotos.EventoId = e.Id AND fotos.rn = 1 " + baseWhere + @"
            ORDER BY
                CASE
                    WHEN e.Nome LIKE @Pattern THEN 0
                    WHEN e.GeneroMusical LIKE @Pattern THEN 1
                    WHEN e.Local LIKE @Pattern THEN 2
                    ELSE 3
                END,
                e.DataEvento ASC
            OFFSET @Offset ROWS FETCH NEXT @TamanhoPagina ROWS ONLY";

        var itens = await conn.QueryAsync<TicketEvent>(sql,
            new { Genero = genero, DataMin = dataMin, DataMax = dataMax, Pattern = likePattern,
                  Cidade = $"%{cidade}%",
                  Offset = offset, TamanhoPagina = tamanhoPagina });

        return new PaginatedResult<TicketEvent>
        {
            Itens = itens,
            Total = total,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina
        };
    }

    /// <summary>
    /// Busca sem termo de nome: apenas filtros gênero/data, ordenado por data.
    /// </summary>
    private async Task<PaginatedResult<TicketEvent>> BuscarSemNomeAsync(
        IDbConnection conn, string? genero, DateTime? dataMin, DateTime? dataMax,
        string? cidade,
        int offset, int pagina, int tamanhoPagina)
    {
        var cidadeCond = string.IsNullOrWhiteSpace(cidade) ? "" : " AND Cidade LIKE @Cidade";

        var baseWhere = $@"
            WHERE DataEvento > GETDATE()
              AND CapacidadeRestante > 0
              AND Status = 'Publicado'
              AND (@Genero IS NULL OR GeneroMusical = @Genero)
              AND (@DataMin IS NULL OR DataEvento >= @DataMin)
              AND (@DataMax IS NULL OR DataEvento <= @DataMax){cidadeCond}";

        var countSql = "SELECT COUNT(*) FROM Eventos " + baseWhere;
        var total = await conn.QuerySingleAsync<int>(countSql,
            new { Genero = genero, DataMin = dataMin, DataMax = dataMax, Cidade = $"%{cidade}%" });

        var sql = @"
            SELECT e.*, fotos.ThumbnailBase64 AS FotoThumbnailBase64,
                   COALESCE((SELECT AVG(CAST(a.Nota AS FLOAT)) FROM Avaliacoes a WHERE a.EventoId = e.Id), 0) AS NotaMedia
            FROM Eventos e
            LEFT JOIN (
                SELECT EventoId, ThumbnailBase64,
                       ROW_NUMBER() OVER (PARTITION BY EventoId ORDER BY Id) AS rn
                FROM EventoFotos
                WHERE ThumbnailBase64 IS NOT NULL
            ) fotos ON fotos.EventoId = e.Id AND fotos.rn = 1 " + baseWhere + @"
            ORDER BY e.DataEvento ASC
            OFFSET @Offset ROWS FETCH NEXT @TamanhoPagina ROWS ONLY";

        var itens = await conn.QueryAsync<TicketEvent>(sql,
            new { Genero = genero, DataMin = dataMin, DataMax = dataMax,
                  Cidade = $"%{cidade}%",
                  Offset = offset, TamanhoPagina = tamanhoPagina });

        return new PaginatedResult<TicketEvent>
        {
            Itens = itens,
            Total = total,
            Pagina = pagina,
            TamanhoPagina = tamanhoPagina
        };
    }

    public async Task<IEnumerable<TicketEvent>> BuscarSugestoesAsync(string termo, int limite = 5)
    {
        using var conn = _connectionFactory.CreateConnection();

        try
        {
            // Tenta full-text search primeiro (FREETEXTTABLE com ranking)
            var sql = @"
                SELECT TOP (@Limite) e.*
                FROM Eventos e
                INNER JOIN FREETEXTTABLE(Eventos, (Nome, Descricao, Local), @Termo) ft
                    ON e.Id = ft.[KEY]
                WHERE e.DataEvento > GETDATE()
                  AND e.CapacidadeRestante > 0
                  AND e.Status = 'Publicado'
                ORDER BY ft.RANK DESC";

            return await conn.QueryAsync<TicketEvent>(sql,
                new { Termo = termo, Limite = limite });
        }
        catch (Exception ex) when (ex.Message.Contains("Full-Text Search", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("full-text", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("FREETEXT", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex,
                "Full-Text Search indisponível para sugestões. Fazendo fallback para LIKE.");

            // Fallback com LIKE (compatível com Express)
            var likePattern = $"%{termo}%";
            var fallbackSql = @"
                SELECT TOP (@Limite) e.*
                FROM Eventos e
                WHERE e.DataEvento > GETDATE()
                  AND e.CapacidadeRestante > 0
                  AND e.Status = 'Publicado'
                  AND (e.Nome LIKE @Pattern OR e.Descricao LIKE @Pattern OR e.Local LIKE @Pattern)
                ORDER BY
                    CASE
                        WHEN e.Nome LIKE @Pattern THEN 0
                        WHEN e.Local LIKE @Pattern THEN 1
                        ELSE 2
                    END,
                    e.DataEvento ASC";

            return await conn.QueryAsync<TicketEvent>(fallbackSql,
                new { Limite = limite, Pattern = likePattern });
        }
    }

    public async Task<TicketEvent?> ObterPorIdAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        string sql = "SELECT * FROM Eventos WHERE Id = @Id";
        return await conn.QueryFirstOrDefaultAsync<TicketEvent>(sql, new { Id = id });
    }

    public async Task<bool> DiminuirCapacidadeAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        string sql = @"UPDATE Eventos 
                       SET CapacidadeRestante = CapacidadeRestante - 1
                       WHERE Id = @EventoId AND CapacidadeRestante > 0";
        var rows = await conn.ExecuteAsync(sql, new { EventoId = eventoId });
        return rows > 0;
    }

    public async Task AumentarCapacidadeAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        string sql = @"UPDATE Eventos 
                       SET CapacidadeRestante = CapacidadeRestante + 1
                       WHERE Id = @EventoId";
        await conn.ExecuteAsync(sql, new { EventoId = eventoId });
    }

    public async Task AtualizarStatusAsync(int id, string status)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "UPDATE Eventos SET Status = @Status WHERE Id = @Id";
        await conn.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public async Task AdicionarFotosAsync(int eventoId, List<EncryptedPhotoDto> fotos)
    {
        if (fotos is null || fotos.Count == 0) return;

        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO EventoFotos (
                EventoId, CiphertextBase64, IvBase64, ChaveAesCifradaBase64,
                ChavePublicaOrgJwk, HashNomeOriginal, TipoMime, TamanhoBytes, Criptografada,
                ThumbnailBase64)
            VALUES (
                @EventoId, @CiphertextBase64, @IvBase64, @ChaveAesCifradaBase64,
                @ChavePublicaOrgJwk, @HashNomeOriginal, @TipoMime, @TamanhoBytes, @Criptografada,
                @ThumbnailBase64)";

        var parameters = fotos.Select(f => new
        {
            EventoId = eventoId,
            f.CiphertextBase64,
            f.IvBase64,
            f.ChaveAesCifradaBase64,
            f.ChavePublicaOrgJwk,
            f.HashNomeOriginal,
            f.TipoMime,
            f.TamanhoBytes,
            f.Criptografada,
            ThumbnailBase64 = (string?)f.ThumbnailBase64
        });

        await conn.ExecuteAsync(sql, parameters);
    }

    public async Task<string?> ObterThumbnailPorEventoAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT TOP 1 ThumbnailBase64 FROM EventoFotos
            WHERE EventoId = @EventoId AND ThumbnailBase64 IS NOT NULL
            ORDER BY Id ASC";
        return await conn.QueryFirstOrDefaultAsync<string>(sql, new { EventoId = eventoId });
    }

    public async Task<List<string>> ObterFotosPorEventoAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT ThumbnailBase64 FROM EventoFotos
            WHERE EventoId = @EventoId AND ThumbnailBase64 IS NOT NULL
            ORDER BY Id ASC";
        var fotos = await conn.QueryAsync<string>(sql, new { EventoId = eventoId });
        return fotos.ToList();
    }

    public async Task<bool> DeletarAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        // Só permite excluir se não houver reservas ativas
        const string check = "SELECT COUNT(*) FROM Reservas WHERE EventoId = @Id";
        var total = await conn.QuerySingleAsync<int>(check, new { Id = id });
        if (total > 0) return false;

        const string sql = "DELETE FROM Eventos WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Tipos de Ingresso (Setores)
    // ══════════════════════════════════════════════════════════════════════

    public async Task AdicionarTiposIngressoAsync(int eventoId, List<TicketType> tipos)
    {
        if (tipos is null || tipos.Count == 0) return;

        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO TiposIngresso (EventoId, Nome, Descricao, Preco, CapacidadeTotal, CapacidadeRestante, Ordem)
            VALUES (@EventoId, @Nome, @Descricao, @Preco, @CapacidadeTotal, @CapacidadeRestante, @Ordem);
            SELECT CAST(SCOPE_IDENTITY() AS INT)";

        foreach (var tipo in tipos)
        {
            var id = await conn.QuerySingleAsync<int>(sql, new
            {
                EventoId = eventoId,
                tipo.Nome,
                tipo.Descricao,
                tipo.Preco,
                tipo.CapacidadeTotal,
                tipo.CapacidadeRestante,
                tipo.Ordem
            });
            tipo.Id = id;
        }
    }

    public async Task<List<TicketType>> ObterTiposIngressoPorEventoAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM TiposIngresso
            WHERE EventoId = @EventoId
            ORDER BY Ordem ASC, Nome ASC";
        var tipos = await conn.QueryAsync<TicketType>(sql, new { EventoId = eventoId });
        return tipos.ToList();
    }

    public async Task<TicketType?> ObterTipoIngressoPorIdAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM TiposIngresso WHERE Id = @Id";
        return await conn.QueryFirstOrDefaultAsync<TicketType>(sql, new { Id = id });
    }

    public async Task<bool> DiminuirCapacidadeTipoIngressoAsync(int ticketTypeId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE TiposIngresso
            SET CapacidadeRestante = CapacidadeRestante - 1
            WHERE Id = @Id AND CapacidadeRestante > 0";
        var rows = await conn.ExecuteAsync(sql, new { Id = ticketTypeId });
        return rows > 0;
    }

    public async Task AumentarCapacidadeTipoIngressoAsync(int ticketTypeId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE TiposIngresso
            SET CapacidadeRestante = CapacidadeRestante + 1
            WHERE Id = @Id";
        await conn.ExecuteAsync(sql, new { Id = ticketTypeId });
    }

    // ══════════════════════════════════════════════════════════════════════
    // Lotes Progressivos
    // ══════════════════════════════════════════════════════════════════════

    public async Task AdicionarLotesAsync(int eventoId, List<Lote> lotes)
    {
        if (lotes is null || lotes.Count == 0) return;

        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO Lotes (EventoId, TicketTypeId, Nome, Preco, QuantidadeMaxima, QuantidadeVendida, DataInicio, DataFim, Ativo)
            VALUES (@EventoId, @TicketTypeId, @Nome, @Preco, @QuantidadeMaxima, 0, @DataInicio, @DataFim, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT)";

        foreach (var lote in lotes)
        {
            var id = await conn.QuerySingleAsync<int>(sql, new
            {
                EventoId = eventoId,
                lote.TicketTypeId,
                lote.Nome,
                lote.Preco,
                lote.QuantidadeMaxima,
                lote.DataInicio,
                lote.DataFim
            });
            lote.Id = id;
        }
    }

    public async Task<List<Lote>> ObterLotesPorEventoAsync(int eventoId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT * FROM Lotes
            WHERE EventoId = @EventoId
            ORDER BY Preco ASC, DataInicio ASC";
        var lotes = await conn.QueryAsync<Lote>(sql, new { EventoId = eventoId });
        return lotes.ToList();
    }

    public async Task<Lote?> ObterLotePorIdAsync(int id)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = "SELECT * FROM Lotes WHERE Id = @Id";
        return await conn.QueryFirstOrDefaultAsync<Lote>(sql, new { Id = id });
    }

    public async Task IncrementarQuantidadeVendidaLoteAsync(int loteId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Lotes
            SET QuantidadeVendida = QuantidadeVendida + 1
            WHERE Id = @Id AND QuantidadeVendida < QuantidadeMaxima";
        await conn.ExecuteAsync(sql, new { Id = loteId });
    }

    public async Task DecrementarQuantidadeVendidaLoteAsync(int loteId)
    {
        using var conn = _connectionFactory.CreateConnection();
        const string sql = @"
            UPDATE Lotes
            SET QuantidadeVendida = CASE
                WHEN QuantidadeVendida > 0 THEN QuantidadeVendida - 1
                ELSE 0 END
            WHERE Id = @Id";
        await conn.ExecuteAsync(sql, new { Id = loteId });
    }

    public async Task AtualizarImagemUrlAsync(int eventoId, string imagemUrl)
    {
        using var conn = _connectionFactory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Eventos SET ImagemUrl = @Url WHERE Id = @Id",
            new { Url = imagemUrl, Id = eventoId });
    }

    // ══════════════════════════════════════════════════════════════════════
    // Criação transacional de evento (evita registros órfãos)
    // ══════════════════════════════════════════════════════════════════════

    public async Task<int> CriarEventoComTransacaoAsync(
        TicketEvent evento, List<TicketType>? tipos, List<Lote>? lotes, List<EncryptedPhotoDto>? fotos)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Insere o evento
            const string sqlEvento = @"
                INSERT INTO Eventos (
                    Nome, CapacidadeTotal, CapacidadeRestante, DataEvento, DataTermino, PrecoPadrao,
                    LimiteIngressosPorUsuario, Local, Descricao, GeneroMusical, EventoGratuito,
                    Status, TaxaServico, TemMeiaEntrada, OrganizadorCpf, Categoria, ImagemUrl)
                OUTPUT INSERTED.Id
                VALUES (
                    @Nome, @CapacidadeTotal, @CapacidadeTotal, @DataEvento, @DataTermino, @PrecoPadrao,
                    @LimiteIngressosPorUsuario, @Local, @Descricao, @GeneroMusical, @EventoGratuito,
                    @Status, @TaxaServico, @TemMeiaEntrada, @OrganizadorCpf, @Categoria, @ImagemUrl)";

            var eventoId = await connection.QuerySingleAsync<int>(sqlEvento, evento, transaction);
            evento.Id = eventoId;

            // 2. Insere tipos de ingresso (setores)
            if (tipos is { Count: > 0 })
            {
                const string sqlTipo = @"
                    INSERT INTO TiposIngresso (EventoId, Nome, Descricao, Preco, CapacidadeTotal, CapacidadeRestante, Ordem)
                    VALUES (@EventoId, @Nome, @Descricao, @Preco, @CapacidadeTotal, @CapacidadeRestante, @Ordem);
                    SELECT CAST(SCOPE_IDENTITY() AS INT)";

                foreach (var tipo in tipos)
                {
                    var tipoId = await connection.QuerySingleAsync<int>(sqlTipo, new
                    {
                        EventoId = eventoId,
                        tipo.Nome,
                        tipo.Descricao,
                        tipo.Preco,
                        tipo.CapacidadeTotal,
                        tipo.CapacidadeRestante,
                        tipo.Ordem
                    }, transaction);
                    tipo.Id = tipoId;
                }
            }

            // 3. Insere lotes progressivos
            if (lotes is { Count: > 0 })
            {
                // Associa lotes específicos aos tipos de ingresso criados
                foreach (var lote in lotes)
                {
                    if (lote.TicketTypeId.HasValue && lote.TicketTypeId > 0 && tipos is { Count: > 0 })
                    {
                        var ticketTypeIndex = lote.TicketTypeId.Value - 1;
                        if (ticketTypeIndex >= 0 && ticketTypeIndex < tipos.Count)
                            lote.TicketTypeId = tipos[ticketTypeIndex].Id;
                    }
                }

                const string sqlLote = @"
                    INSERT INTO Lotes (EventoId, TicketTypeId, Nome, Preco, QuantidadeMaxima, QuantidadeVendida, DataInicio, DataFim, Ativo)
                    VALUES (@EventoId, @TicketTypeId, @Nome, @Preco, @QuantidadeMaxima, 0, @DataInicio, @DataFim, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS INT)";

                foreach (var lote in lotes)
                {
                    var loteId = await connection.QuerySingleAsync<int>(sqlLote, new
                    {
                        EventoId = eventoId,
                        lote.TicketTypeId,
                        lote.Nome,
                        lote.Preco,
                        lote.QuantidadeMaxima,
                        lote.DataInicio,
                        lote.DataFim
                    }, transaction);
                    lote.Id = loteId;
                }
            }

            // 4. Insere fotos criptografadas
            if (fotos is { Count: > 0 })
            {
                const string sqlFoto = @"
                    INSERT INTO EventoFotos (
                        EventoId, CiphertextBase64, IvBase64, ChaveAesCifradaBase64,
                        ChavePublicaOrgJwk, HashNomeOriginal, TipoMime, TamanhoBytes, Criptografada,
                        ThumbnailBase64)
                    VALUES (
                        @EventoId, @CiphertextBase64, @IvBase64, @ChaveAesCifradaBase64,
                        @ChavePublicaOrgJwk, @HashNomeOriginal, @TipoMime, @TamanhoBytes, @Criptografada,
                        @ThumbnailBase64)";

                var fotoParams = fotos.Select(f => new
                {
                    EventoId = eventoId,
                    f.CiphertextBase64,
                    f.IvBase64,
                    f.ChaveAesCifradaBase64,
                    f.ChavePublicaOrgJwk,
                    f.HashNomeOriginal,
                    f.TipoMime,
                    f.TamanhoBytes,
                    f.Criptografada,
                    ThumbnailBase64 = (string?)f.ThumbnailBase64
                });

                await connection.ExecuteAsync(sqlFoto, fotoParams, transaction);
            }

            transaction.Commit();
            return eventoId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
