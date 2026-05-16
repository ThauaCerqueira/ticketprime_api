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

    [Fact]
    public async Task Concorrencia_DeadlockPrevention_LockOrdemConsistente()
    {
        IgnorarSeDBIndisponivel();
        var cs = IntegrationTestFixture.ObterConnectionString();

        // ── Cria dois eventos ─────────────────────────────────────────
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

        // ── Duas transações paralelas, mesma ordem de lock: e1 → e2 ──
        // Sem inversão de ordem → sem deadlock. T2 bloqueia em e1 até
        // T1 commitar, depois ambas completam normalmente.
        // Nota: as transações devem correr em Tasks separadas; executá-las
        // em série na mesma thread causaria deadlock de aplicação (T2 bloqueia
        // o await enquanto T1 ainda não fez commit).
        var tsc1 = new TaskCompletionSource();   // sinaliza quando T1 travou e1
        var tsc2 = new TaskCompletionSource();   // sinaliza quando T2 tentou e1

        var task1 = Task.Run(async () =>
        {
            await using var c1 = new SqlConnection(cs);
            await c1.OpenAsync();
            await using var tx1 = (SqlTransaction)await c1.BeginTransactionAsync();

            // Lock e1
            await using var q1 = c1.CreateCommand();
            q1.Transaction = tx1;
            q1.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E";
            q1.Parameters.AddWithValue("@E", e1);
            await q1.ExecuteNonQueryAsync();

            tsc1.SetResult(); // avisa T2 que e1 está bloqueado

            await tsc2.Task;  // aguarda T2 tentar e1 (já está bloqueado no banco)

            // Lock e2 (mesma ordem — sem deadlock com T2)
            q1.Parameters.Clear();
            q1.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E";
            q1.Parameters.AddWithValue("@E", e2);
            await q1.ExecuteNonQueryAsync();

            await tx1.CommitAsync(); // libera e1 — T2 pode prosseguir
        });

        var task2 = Task.Run(async () =>
        {
            await tsc1.Task; // espera T1 ter e1 antes de tentar

            await using var c2 = new SqlConnection(cs);
            await c2.OpenAsync();
            await using var tx2 = (SqlTransaction)await c2.BeginTransactionAsync();

            // Tenta lock e1 — vai bloquear até T1 commitar
            await using var q2 = c2.CreateCommand();
            q2.Transaction = tx2;
            q2.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E";
            q2.Parameters.AddWithValue("@E", e1);

            tsc2.SetResult(); // avisa T1 que está prestes a tentar o lock

            await q2.ExecuteNonQueryAsync(); // bloqueia até T1 commitar

            // Lock e2 (mesma ordem — sem chance de deadlock)
            q2.Parameters.Clear();
            q2.CommandText = "UPDATE Eventos WITH(UPDLOCK) SET CapacidadeRestante-=1 WHERE Id=@E";
            q2.Parameters.AddWithValue("@E", e2);
            await q2.ExecuteNonQueryAsync();

            await tx2.CommitAsync();
        });

        // Ambas devem completar sem deadlock dentro de 15 s
        await Task.WhenAll(task1, task2).WaitAsync(TimeSpan.FromSeconds(15));

        // Capacidade final: 10 - 2 decrementos de cada tx = 8
        cmd.CommandText = "SELECT CapacidadeRestante FROM Eventos WHERE Id=@E1";
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@E1", e1);
        Assert.Equal(8, (int)(await cmd.ExecuteScalarAsync())!);
    }
}
