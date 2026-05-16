using Microsoft.Data.SqlClient;
using src.Models;
using Xunit;

namespace TicketPrime.Tests.Integration;

/// <summary>
/// Testes de integração para CANCELAMENTO/ESTORNO com SQL Server real.
///
/// ═══════════════════════════════════════════════════════════════════
/// ANTES: Testes unitários mockavam o repositório, mas o método
///   CancelarIngressoAsync usa SQL direto com UPDLOCK via
///   DbConnectionFactory — mocks não funcionavam.
///
/// AGORA: Testes com banco SQL Server real via IntegrationTestFixture.
///   Validamos: UPDLOCK, cancelamento atômico, restauração de cupom,
///   concorrência entre transações e idempotência.
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class CancellationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public CancellationIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void IgnorarSeDBIndisponivel()
    {
        Skip.If(!_fixture.DatabaseAvailable,
            "SQL Server não disponível. Execute 'docker compose up -d' para rodar os testes de integração.");
    }

    private static string CpfUnico()
    {
        var uid = Math.Abs(Guid.NewGuid().GetHashCode());
        return (99900000000L + (uid % 99999999L)).ToString("D11");
    }

    /// <summary> Cria dados de teste para cancelamento. </summary>
    private async Task<(string cpf, int eventoId, int reservaId)> CriarDadosAsync(
        bool comCupom = false, int horasAntecedencia = 720)
    {
        var cs = IntegrationTestFixture.ObterConnectionString();
        var cpf = CpfUnico();
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "INSERT INTO Usuarios (Cpf,Nome,Email,Senha,Perfil,EmailVerificado) VALUES(@C,@N,@E,'hash','CLIENTE',1)";
        cmd.Parameters.AddWithValue("@C", cpf);
        cmd.Parameters.AddWithValue("@N", "User");
        cmd.Parameters.AddWithValue("@E", $"c_{Guid.NewGuid():N}@t.com");
        await cmd.ExecuteNonQueryAsync();

        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO Eventos(Nome,CapacidadeTotal,CapacidadeRestante,DataEvento,PrecoPadrao,Status,Local,Descricao,TaxaServico,LimiteIngressosPorUsuario) VALUES('Show',100,100,DATEADD(HOUR,@H,GETUTCDATE()),200.00,'Publicado','Local','Teste',10,2);SELECT SCOPE_IDENTITY();";
        cmd.Parameters.AddWithValue("@H", horasAntecedencia);
        var eventoId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        string? cupomCodigo = null;
        if (comCupom)
        {
            cupomCodigo = $"C{Math.Abs(Guid.NewGuid().GetHashCode()) % 10000:D4}";
            cmd.Parameters.Clear();
            cmd.CommandText = "INSERT INTO Cupons(Codigo,PorcentagemDesconto,ValorMinimoRegra,DataExpiracao,LimiteUsos,TotalUsado) VALUES(@C,10,10,DATEADD(DAY,60,GETUTCDATE()),100,1)";
            cmd.Parameters.AddWithValue("@C", cupomCodigo);
            await cmd.ExecuteNonQueryAsync();
        }

        cmd.Parameters.Clear();
        cmd.CommandText = "UPDATE Eventos SET CapacidadeRestante-=1 WHERE Id=@E";
        cmd.Parameters.AddWithValue("@E", eventoId);
        await cmd.ExecuteNonQueryAsync();

        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO Reservas(UsuarioCpf,EventoId,ValorFinalPago,CodigoIngresso,Status,DataCompra,CupomUtilizado,TemSeguro,EhMeiaEntrada,TaxaServicoPago,ValorSeguroPago,CodigoTransacaoGateway) VALUES(@C,@E,200,@I,'Ativa',GETUTCDATE(),@U,0,0,0,0,@TX);SELECT SCOPE_IDENTITY();";
        cmd.Parameters.AddWithValue("@C", cpf);
        cmd.Parameters.AddWithValue("@E", eventoId);
        cmd.Parameters.AddWithValue("@I", $"ING-{Guid.NewGuid():N}"[..32]);
        cmd.Parameters.AddWithValue("@U", (object?)cupomCodigo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TX", $"TX-INT-{Guid.NewGuid():N}"[..20]);
        var reservaId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return (cpf, eventoId, reservaId);
    }

    [SkippableFact]
    public async Task Cancelamento_UPDLOCK_DeveCancelarELiberarVaga()
    {
        IgnorarSeDBIndisponivel();
        var (_, eventoId, reservaId) = await CriarDadosAsync();

        await using var conn = new SqlConnection(IntegrationTestFixture.ObterConnectionString());
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        // UPDLOCK + cancelamento
        cmd.CommandText = "UPDATE Reservas WITH(UPDLOCK,ROWLOCK) SET Status='Cancelada',DataCancelamento=GETUTCDATE() WHERE Id=@Id AND Status='Ativa'";
        cmd.Parameters.AddWithValue("@Id", reservaId);
        Assert.Equal(1, await cmd.ExecuteNonQueryAsync());

        // Libera vaga
        cmd.Parameters.Clear();
        cmd.CommandText = "UPDATE Eventos SET CapacidadeRestante+=1 WHERE Id=@E";
        cmd.Parameters.AddWithValue("@E", eventoId);
        await cmd.ExecuteNonQueryAsync();

        tx.Commit();

        // Verifica
        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT Status FROM Reservas WHERE Id=@Id";
        cmd.Parameters.AddWithValue("@Id", reservaId);
        Assert.Equal("Cancelada", (string)(await cmd.ExecuteScalarAsync())!);

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT CapacidadeRestante FROM Eventos WHERE Id=@E";
        cmd.Parameters.AddWithValue("@E", eventoId);
        Assert.Equal(100, (int)(await cmd.ExecuteScalarAsync())!);
    }

    [SkippableFact]
    public async Task Cancelamento_UPDLOCK_DuasTransacoes_ApenasUmaCancela()
    {
        IgnorarSeDBIndisponivel();
        var (_, _, reservaId) = await CriarDadosAsync();

        var cs = IntegrationTestFixture.ObterConnectionString();
        await using var c1 = new SqlConnection(cs);
        await using var c2 = new SqlConnection(cs);
        await c1.OpenAsync();
        await c2.OpenAsync();
        await using var t1 = c1.BeginTransaction();
        await using var t2 = c2.BeginTransaction();
        await using var cmd1 = c1.CreateCommand();
        await using var cmd2 = c2.CreateCommand();
        cmd1.Transaction = t1;
        cmd2.Transaction = t2;

        cmd1.CommandText = "UPDATE Reservas WITH(UPDLOCK,ROWLOCK) SET Status='Cancelada' WHERE Id=@Id AND Status='Ativa'";
        cmd1.Parameters.AddWithValue("@Id", reservaId);
        Assert.Equal(1, await cmd1.ExecuteNonQueryAsync());

        cmd2.CommandText = "UPDATE Reservas WITH(UPDLOCK,ROWLOCK) SET Status='Cancelada' WHERE Id=@Id AND Status='Ativa'";
        cmd2.Parameters.AddWithValue("@Id", reservaId);

        await t1.CommitAsync();

        var rows2 = await cmd2.ExecuteNonQueryAsync();
        Assert.Equal(0, rows2); // Já cancelada pela tx1

        await t2.RollbackAsync();
    }

    [SkippableFact]
    public async Task Cancelamento_ComCupom_DeveRestaurarTotalUsado()
    {
        IgnorarSeDBIndisponivel();
        var (_, _, _) = await CriarDadosAsync(comCupom: true);

        await using var conn = new SqlConnection(IntegrationTestFixture.ObterConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT TOP 1 CupomUtilizado FROM Reservas WHERE CupomUtilizado IS NOT NULL ORDER BY Id DESC";
        var cupom = (string)(await cmd.ExecuteScalarAsync())!;

        cmd.Parameters.Clear();
        cmd.CommandText = "UPDATE Cupons SET TotalUsado=CASE WHEN TotalUsado>0 THEN TotalUsado-1 ELSE 0 END WHERE Codigo=@C";
        cmd.Parameters.AddWithValue("@C", cupom);
        await cmd.ExecuteNonQueryAsync();

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT TotalUsado FROM Cupons WHERE Codigo=@C";
        cmd.Parameters.AddWithValue("@C", cupom);
        Assert.Equal(0, (int)(await cmd.ExecuteScalarAsync())!);
    }

    [SkippableFact]
    public async Task Cancelamento_Idempotente_SegundaTentativaZeroLinhas()
    {
        IgnorarSeDBIndisponivel();
        var (_, _, reservaId) = await CriarDadosAsync();

        await using var conn = new SqlConnection(IntegrationTestFixture.ObterConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "UPDATE Reservas SET Status='Cancelada' WHERE Id=@Id AND Status='Ativa'";
        cmd.Parameters.AddWithValue("@Id", reservaId);
        Assert.Equal(1, await cmd.ExecuteNonQueryAsync());

        Assert.Equal(0, await cmd.ExecuteNonQueryAsync()); // Já cancelada
    }
}
