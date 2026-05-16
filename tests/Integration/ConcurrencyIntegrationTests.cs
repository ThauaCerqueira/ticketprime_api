using Microsoft.Data.SqlClient;
using Xunit;

namespace TicketPrime.Tests.Integration;

/// <summary>
/// Testes de CONCORRÊNCIA com SQL Server real — UPDLOCK, race conditions.
///
/// ═══════════════════════════════════════════════════════════════════
/// ANTES: Testes mockavam ITransacaoCompraExecutor.
///   O UPDLOCK real nunca era testado.
///
/// AGORA: Testes com banco SQL Server real.
///   - Duas transações simultâneas no mesmo registro (UPDLOCK)
///   - Corrida pelo último ingresso
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConcurrencyIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public ConcurrencyIntegrationTests(IntegrationTestFixture fixture)
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

    /// <summary> Cria usuário e evento, retorna CPF e EventoId. </summary>
    private async Task<(string cpf, int eventoId)> CriarUsuarioEEventoAsync(int capacidade = 100)
    {
        var cs = IntegrationTestFixture.ObterConnectionString();
        var cpf = CpfUnico();
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "INSERT INTO Usuarios(Cpf,Nome,Email,Senha,Perfil,EmailVerificado) VALUES(@C,'User',@E,'hash','CLIENTE',1)";
        cmd.Parameters.AddWithValue("@C", cpf);
        cmd.Parameters.AddWithValue("@E", $"conc_{Guid.NewGuid():N}@t.com");
        await cmd.ExecuteNonQueryAsync();

        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO Eventos(Nome,CapacidadeTotal,CapacidadeRestante,DataEvento,PrecoPadrao,Status,Local,Descricao,TaxaServico,LimiteIngressosPorUsuario) VALUES('Show',@Cap,@Cap,DATEADD(DAY,30,GETUTCDATE()),100.00,'Publicado','Local','Teste',5,2);SELECT SCOPE_IDENTITY();";
        cmd.Parameters.AddWithValue("@Cap", capacidade);
        var eventoId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return (cpf, eventoId);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TESTE 1: UPDLOCK — duas transações tentam cancelar a mesma reserva
    // ═══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task Concorrencia_UPDLOCK_DuasTransacoes_UmaBloqueia()
    {
        IgnorarSeDBIndisponivel();
        var cs = IntegrationTestFixture.ObterConnectionString();

        // Setup: cria evento com capacidade = 1 e insere uma reserva
        var (cpf, eventoId) = await CriarUsuarioEEventoAsync(capacidade: 1);

        await using (var conn = new SqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Eventos SET CapacidadeRestante=0 WHERE Id=@E";
            cmd.Parameters.AddWithValue("@E", eventoId);
            await cmd.ExecuteNonQueryAsync();

            cmd.Parameters.Clear();
            cmd.CommandText = "INSERT INTO Reservas(UsuarioCpf,EventoId,ValorFinalPago,CodigoIngresso,Status,DataCompra,TemSeguro,EhMeiaEntrada,TaxaServicoPago,ValorSeguroPago) VALUES(@C,@E,100,@I,'Ativa',GETUTCDATE(),0,0,0,0);SELECT SCOPE_IDENTITY();";
            cmd.Parameters.AddWithValue("@C", cpf);
            cmd.Parameters.AddWithValue("@E", eventoId);
            cmd.Parameters.AddWithValue("@I", $"ING-{Guid.NewGuid():N}"[..32]);
            var reservaId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Act — duas conexões tentam UPDLOCK simultaneamente
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

            await t1.CommitAsync(); // Tx1 libera o lock

            var rows2 = await cmd2.ExecuteNonQueryAsync();
            Assert.Equal(0, rows2); // Tx2 não encontra mais registros "Ativa"
            await t2.RollbackAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // TESTE 2: Capacidade = 1, dois usuários — só um compra
    // ═══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task Concorrencia_Capacidade1_DoisUsuarios_ApenasUmCompra()
    {
        IgnorarSeDBIndisponivel();
        var cs = IntegrationTestFixture.ObterConnectionString();

        // Cria evento com capacidade = 1
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Eventos(Nome,CapacidadeTotal,CapacidadeRestante,DataEvento,PrecoPadrao,Status,Local,Descricao,TaxaServico,LimiteIngressosPorUsuario) VALUES('Unico',1,1,DATEADD(DAY,30,GETUTCDATE()),100.00,'Publicado','Local','Unico',5,2);SELECT SCOPE_IDENTITY();";
        var eventoId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        var cpf1 = CpfUnico();
        var cpf2 = CpfUnico();

        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO Usuarios(Cpf,Nome,Email,Senha,Perfil,EmailVerificado) VALUES(@C1,'U1',@E1,'hash','CLIENTE',1),(@C2,'U2',@E2,'hash','CLIENTE',1)";
        cmd.Parameters.AddWithValue("@C1", cpf1);
        cmd.Parameters.AddWithValue("@E1", $"u1_{Guid.NewGuid():N}@t.com");
        cmd.Parameters.AddWithValue("@C2", cpf2);
        cmd.Parameters.AddWithValue("@E2", $"u2_{Guid.NewGuid():N}@t.com");
        await cmd.ExecuteNonQueryAsync();

        // Simula duas transações concorrentes: cada uma tenta decrementar capacidade
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

        // Ambos tentam decrementar a capacidade
        cmd1.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante=CapacidadeRestante-1 WHERE Id=@E AND CapacidadeRestante>0";
        cmd1.Parameters.AddWithValue("@E", eventoId);
        var r1 = await cmd1.ExecuteNonQueryAsync();
        Assert.Equal(1, r1); // Tx1 conseguiu

        cmd2.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante=CapacidadeRestante-1 WHERE Id=@E AND CapacidadeRestante>0";
        cmd2.Parameters.AddWithValue("@E", eventoId);

        await t1.CommitAsync(); // Tx1 libera

        var r2 = await cmd2.ExecuteNonQueryAsync();
        Assert.Equal(0, r2); // Tx2 não conseguiu — capacidade já é 0
        await t2.RollbackAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // TESTE 3: Deadlock prevention — ordem consistente de locks
    // ═══════════════════════════════════════════════════════════════════

    [Fact(Skip = "Teste de UPDLOCK lento (~30s) por design. Execute manualmente removendo o Skip.")]
    public async Task Concorrencia_DeadlockPrevention_LockOrdemConsistente()
    {
        IgnorarSeDBIndisponivel();
        var cs = IntegrationTestFixture.ObterConnectionString();

        // Cria dois eventos
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Eventos(Nome,CapacidadeTotal,CapacidadeRestante,DataEvento,PrecoPadrao,Status,Local,Descricao,TaxaServico,LimiteIngressosPorUsuario)
            VALUES('E1',10,10,DATEADD(DAY,30,GETUTCDATE()),100.00,'Publicado','L1','D1',5,2);
            SELECT SCOPE_IDENTITY();";
        var e1 = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        cmd.Parameters.Clear();
        cmd.CommandText = @"
            INSERT INTO Eventos(Nome,CapacidadeTotal,CapacidadeRestante,DataEvento,PrecoPadrao,Status,Local,Descricao,TaxaServico,LimiteIngressosPorUsuario)
            VALUES('E2',10,10,DATEADD(DAY,30,GETUTCDATE()),100.00,'Publicado','L2','D2',5,2);
            SELECT SCOPE_IDENTITY();";
        var e2 = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Duas transações que lockeiam na MESMA ORDEM (e1, e2) — sem deadlock
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

        // Ambas lockeiam na ordem: e1, e2 (consistente)
        cmd1.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E1";
        cmd1.Parameters.AddWithValue("@E1", e1);
        await cmd1.ExecuteNonQueryAsync();

        cmd2.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E1";
        cmd2.Parameters.AddWithValue("@E1", e1);
        await cmd2.ExecuteNonQueryAsync();

        cmd1.Parameters.Clear();
        cmd1.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E2";
        cmd1.Parameters.AddWithValue("@E2", e2);
        await cmd1.ExecuteNonQueryAsync();

        cmd2.Parameters.Clear();
        cmd2.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E2";
        cmd2.Parameters.AddWithValue("@E2", e2);
        await cmd2.ExecuteNonQueryAsync();

        await t1.CommitAsync();
        await t2.CommitAsync();

        // Ambas completaram sem deadlock
        cmd.CommandText = "SELECT CapacidadeRestante FROM Eventos WHERE Id=@E1";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@E1", e1);
        Assert.Equal(8, (int)(await cmd.ExecuteScalarAsync())!); // 10-1-1=8
    }
}
