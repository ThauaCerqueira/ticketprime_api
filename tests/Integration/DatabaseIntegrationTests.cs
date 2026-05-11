using src.Models;
using Xunit;

namespace TicketPrime.Tests.Integration;

/// <summary>
/// Testes de integração com banco SQL Server real.
///
/// Para executar:
///   1. docker compose up -d sqlserver   (sobe o banco)
///   2. dotnet test --filter "Category=Integration"
///
/// Cada teste roda dentro de uma transação que é revertida ao final,
/// garantindo isolamento total entre os testes.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class DatabaseIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public DatabaseIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // USUÁRIOS
    // ═══════════════════════════════════════════════════════════════════════

    private static User CriarUsuarioTeste()
    {
        var uid = Math.Abs(Guid.NewGuid().GetHashCode());
        var cpf = (99900000000L + (uid % 100000000L)).ToString("D11");
        return new User
        {
            Cpf = cpf,
            Nome = "Usuário Teste",
            Email = $"teste_{uid}@ticketprime.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("Teste@123", workFactor: 11),
            Perfil = "CLIENTE",
            SenhaTemporaria = false,
            EmailVerificado = false
        };
    }

    [Fact]
    public async Task Usuario_CriarEObterPorCpf_DeveRetornarUsuario()
    {
        var usuario = CriarUsuarioTeste();

        var cpfCriado = await _fixture.UsuarioRepository.CriarUsuario(usuario);
        var obtido = await _fixture.UsuarioRepository.ObterPorCpf(usuario.Cpf);

        Assert.Equal(usuario.Cpf, cpfCriado);
        Assert.NotNull(obtido);
        Assert.Equal(usuario.Nome, obtido.Nome);
        Assert.Equal(usuario.Email, obtido.Email);
        Assert.Equal("CLIENTE", obtido.Perfil);
    }

    [Fact]
    public async Task Usuario_ObterPorEmail_DeveRetornarUsuario()
    {
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var obtido = await _fixture.UsuarioRepository.ObterPorEmail(usuario.Email);

        Assert.NotNull(obtido);
        Assert.Equal(usuario.Cpf, obtido.Cpf);
    }

    [Fact]
    public async Task Usuario_ObterPorEmailInexistente_DeveRetornarNull()
    {
        var obtido = await _fixture.UsuarioRepository.ObterPorEmail("naoexiste@teste.com");

        Assert.Null(obtido);
    }

    [Fact]
    public async Task Usuario_AtualizarSenha_DevePersistirNovaSenha()
    {
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);
        var novaSenhaHash = BCrypt.Net.BCrypt.HashPassword("NovaSenha@456", workFactor: 11);

        await _fixture.UsuarioRepository.AtualizarSenha(usuario.Cpf, novaSenhaHash);
        var obtido = await _fixture.UsuarioRepository.ObterPorCpf(usuario.Cpf);

        Assert.NotNull(obtido);
        Assert.Equal(novaSenhaHash, obtido.Senha);
    }

    [Fact]
    public async Task Usuario_ConfirmarEmail_DeveMarcarComoVerificado()
    {
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        await _fixture.UsuarioRepository.ConfirmarEmail(usuario.Email);
        var obtido = await _fixture.UsuarioRepository.ObterPorCpf(usuario.Cpf);

        Assert.NotNull(obtido);
        Assert.True(obtido.EmailVerificado);
    }

    [Fact]
    public async Task Usuario_SalvarTokenVerificacaoEmail_DevePersistirToken()
    {
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);
        var token = "tokenteste123";
        var expiracao = DateTime.UtcNow.AddHours(24);

        await _fixture.UsuarioRepository.SalvarTokenVerificacaoEmail(usuario.Email, token, expiracao);
        var obtido = await _fixture.UsuarioRepository.ObterPorCpf(usuario.Cpf);

        Assert.NotNull(obtido);
        Assert.Equal(token, obtido.TokenVerificacaoEmail);
        Assert.NotNull(obtido.TokenExpiracaoEmail);
        Assert.True(obtido.TokenExpiracaoEmail.Value - expiracao < TimeSpan.FromSeconds(1));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTOS
    // ═══════════════════════════════════════════════════════════════════════

    private static TicketEvent CriarEventoTeste() => new()
    {
        Nome = "Show de Teste",
        DataEvento = DateTime.Now.AddDays(30),
        Local = "Teatro Teste, São Paulo",
        Descricao = "Evento criado por teste de integração.",
        GeneroMusical = "Rock",
        PrecoPadrao = 100.00m,
        CapacidadeTotal = 500,
        Status = "Rascunho",
        TaxaServico = 5.00m,
        LimiteIngressosPorUsuario = 6
    };

    [Fact]
    public async Task Evento_CriarEObterPorId_DeveRetornarEvento()
    {
        var evento = CriarEventoTeste();

        var id = await _fixture.EventoRepository.AdicionarAsync(evento);
        var obtido = await _fixture.EventoRepository.ObterPorIdAsync(id);

        Assert.True(id > 0);
        Assert.NotNull(obtido);
        Assert.Equal(evento.Nome, obtido.Nome);
        Assert.Equal(evento.PrecoPadrao, obtido.PrecoPadrao);
        Assert.Equal(evento.CapacidadeTotal, obtido.CapacidadeTotal);
        Assert.Equal("Rascunho", obtido.Status);
    }

    [Fact]
    public async Task Evento_ObterTodos_DeveRetornarPaginatedResult()
    {
        var evento1 = CriarEventoTeste();
        evento1.Nome = "Evento Teste 1";
        var evento2 = CriarEventoTeste();
        evento2.Nome = "Evento Teste 2";

        await _fixture.EventoRepository.AdicionarAsync(evento1);
        await _fixture.EventoRepository.AdicionarAsync(evento2);

        var resultado = await _fixture.EventoRepository.ObterTodosAsync(pagina: 1, tamanhoPagina: 20);

        Assert.NotNull(resultado);
        Assert.True(resultado.Total >= 2);
        Assert.Contains(resultado.Itens, e => e.Nome == "Evento Teste 1");
        Assert.Contains(resultado.Itens, e => e.Nome == "Evento Teste 2");
    }

    [Fact]
    public async Task Evento_DiminuirCapacidade_DeveReduzirCapacidade()
    {
        var evento = CriarEventoTeste();
        var id = await _fixture.EventoRepository.AdicionarAsync(evento);

        var sucesso = await _fixture.EventoRepository.DiminuirCapacidadeAsync(id);
        var obtido = await _fixture.EventoRepository.ObterPorIdAsync(id);

        Assert.True(sucesso);
        Assert.NotNull(obtido);
        Assert.Equal(evento.CapacidadeTotal - 1, obtido.CapacidadeTotal);
    }

    [Fact]
    public async Task Evento_DiminuirCapacidadeAteZero_DeveRetornarFalso()
    {
        var evento = CriarEventoTeste();
        evento.CapacidadeTotal = 1;
        var id = await _fixture.EventoRepository.AdicionarAsync(evento);

        var primeiro = await _fixture.EventoRepository.DiminuirCapacidadeAsync(id);
        var segundo = await _fixture.EventoRepository.DiminuirCapacidadeAsync(id);

        Assert.True(primeiro);
        Assert.False(segundo);
    }

    [Fact]
    public async Task Evento_AumentarCapacidade_DeveIncrementar()
    {
        var evento = CriarEventoTeste();
        var id = await _fixture.EventoRepository.AdicionarAsync(evento);
        await _fixture.EventoRepository.DiminuirCapacidadeAsync(id);
        var antes = await _fixture.EventoRepository.ObterPorIdAsync(id);

        await _fixture.EventoRepository.AumentarCapacidadeAsync(id);
        var depois = await _fixture.EventoRepository.ObterPorIdAsync(id);

        Assert.NotNull(antes);
        Assert.NotNull(depois);
        Assert.Equal(antes.CapacidadeTotal + 1, depois.CapacidadeTotal);
    }

    [Fact]
    public async Task Evento_AtualizarStatus_DeveAlterarStatus()
    {
        var evento = CriarEventoTeste();
        var id = await _fixture.EventoRepository.AdicionarAsync(evento);

        await _fixture.EventoRepository.AtualizarStatusAsync(id, "Publicado");
        var obtido = await _fixture.EventoRepository.ObterPorIdAsync(id);

        Assert.NotNull(obtido);
        Assert.Equal("Publicado", obtido.Status);
    }

    [Fact]
    public async Task Evento_BuscarDisponiveis_DeveFiltrarPorNome()
    {
        var evento = CriarEventoTeste();
        evento.Nome = "Rock in Rio Teste";
        var id = await _fixture.EventoRepository.AdicionarAsync(evento);
        await _fixture.EventoRepository.AtualizarStatusAsync(id, "Publicado");

        var resultado = await _fixture.EventoRepository.BuscarDisponiveisAsync(
            nome: "Rock", genero: null, dataMin: null, dataMax: null);

        Assert.NotNull(resultado);
        Assert.Contains(resultado.Itens, e => e.Nome.Contains("Rock"));
    }

    [Fact]
    public async Task Evento_Deletar_DeveRemoverEvento()
    {
        var evento = CriarEventoTeste();
        var id = await _fixture.EventoRepository.AdicionarAsync(evento);

        var deletado = await _fixture.EventoRepository.DeletarAsync(id);
        var obtido = await _fixture.EventoRepository.ObterPorIdAsync(id);

        Assert.True(deletado);
        Assert.Null(obtido);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CUPONS
    // ═══════════════════════════════════════════════════════════════════════

    private static Coupon CriarCupomTeste() => new()
    {
        Codigo = "T" + Guid.NewGuid().ToString("N")[..7].ToUpper(),
        PorcentagemDesconto = 10.00m,
        ValorMinimoRegra = 50.00m,
        DataExpiracao = DateTime.Now.AddDays(30),
        LimiteUsos = 0,      // ilimitado
        TotalUsado = 0
    };

    [Fact]
    public async Task Cupom_CriarEObterPorCodigo_DeveRetornarCupom()
    {
        var cupom = CriarCupomTeste();

        var id = await _fixture.CupomRepository.CriarAsync(cupom);
        var obtido = await _fixture.CupomRepository.ObterPorCodigoAsync(cupom.Codigo);

        Assert.True(id > 0);
        Assert.NotNull(obtido);
        Assert.Equal(cupom.Codigo, obtido.Codigo);
        Assert.Equal(cupom.PorcentagemDesconto, obtido.PorcentagemDesconto);
    }

    [Fact]
    public async Task Cupom_IncrementarUso_DeveAumentarTotalUsado()
    {
        var cupom = CriarCupomTeste();
        await _fixture.CupomRepository.CriarAsync(cupom);

        await _fixture.CupomRepository.IncrementarUsoAsync(cupom.Codigo);
        var obtido = await _fixture.CupomRepository.ObterPorCodigoAsync(cupom.Codigo);

        Assert.NotNull(obtido);
        Assert.Equal(1, obtido.TotalUsado);
    }

    [Fact]
    public async Task Cupom_Listar_DeveRetornarTodos()
    {
        var cupom1 = CriarCupomTeste();
        var cupom2 = CriarCupomTeste();

        await _fixture.CupomRepository.CriarAsync(cupom1);
        await _fixture.CupomRepository.CriarAsync(cupom2);

        var lista = await _fixture.CupomRepository.ListarAsync();

        Assert.NotNull(lista);
        Assert.Contains(lista, c => c.Codigo == cupom1.Codigo);
        Assert.Contains(lista, c => c.Codigo == cupom2.Codigo);
    }

    [Fact]
    public async Task Cupom_Deletar_DeveRemoverCupom()
    {
        var cupom = CriarCupomTeste();
        await _fixture.CupomRepository.CriarAsync(cupom);

        var deletado = await _fixture.CupomRepository.DeletarAsync(cupom.Codigo);
        var obtido = await _fixture.CupomRepository.ObterPorCodigoAsync(cupom.Codigo);

        Assert.True(deletado);
        Assert.Null(obtido);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RESERVAS (FLUXO COMPLETO)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reserva_FluxoCompleto_CriarEObter()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var evento = CriarEventoTeste();
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = evento.PrecoPadrao,
            CupomUtilizado = null,
            TemSeguro = false,
            DataCompra = DateTime.Now,
            CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20],
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            ValorSeguroPago = 0m
        };

        // Act
        var criada = await _fixture.ReservaRepository.CriarAsync(reserva);
        var lista = await _fixture.ReservaRepository.ListarPorUsuarioAsync(usuario.Cpf);
        var detalhada = await _fixture.ReservaRepository.ObterDetalhadaPorIdAsync(criada.Id, usuario.Cpf);

        // Assert
        Assert.NotNull(criada);
        Assert.True(criada.Id > 0);

        var listaArr = lista.ToList();
        Assert.NotEmpty(listaArr);
        Assert.Contains(listaArr, r => r.Id == criada.Id);

        Assert.NotNull(detalhada);
        Assert.Equal(evento.Nome, detalhada.Nome);
    }

    [Fact]
    public async Task Reserva_Cancelar_DeveMarcarComoCancelada()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var evento = CriarEventoTeste();
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = evento.PrecoPadrao,
            DataCompra = DateTime.Now,
            CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20],
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            TemSeguro = false,
            ValorSeguroPago = 0m
        };
        var criada = await _fixture.ReservaRepository.CriarAsync(reserva);

        // Act
        var cancelado = await _fixture.ReservaRepository.CancelarAsync(criada.Id, usuario.Cpf);
        var lista = await _fixture.ReservaRepository.ListarPorUsuarioAsync(usuario.Cpf);

        // Assert (soft delete: reserva cancelada ainda aparece na listagem)
        Assert.True(cancelado);
        var listaArr = lista.ToList();
        Assert.Contains(listaArr, r => r.Id == criada.Id && r.Status == "Cancelada");
    }

    [Fact]
    public async Task Reserva_CancelarDeOutroUsuario_DeveFalhar()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var outroUsuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(outroUsuario);

        var evento = CriarEventoTeste();
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = evento.PrecoPadrao,
            DataCompra = DateTime.Now,
            CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20],
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            TemSeguro = false,
            ValorSeguroPago = 0m
        };
        var criada = await _fixture.ReservaRepository.CriarAsync(reserva);

        // Act — tentativa de cancelar com CPF diferente
        var cancelado = await _fixture.ReservaRepository.CancelarAsync(criada.Id, outroUsuario.Cpf);

        // Assert
        Assert.False(cancelado);
    }

    [Fact]
    public async Task Reserva_ContarReservasPorEvento_DeveRetornarTotal()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var evento = CriarEventoTeste();
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = evento.PrecoPadrao,
            DataCompra = DateTime.Now,
            CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20],
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            TemSeguro = false,
            ValorSeguroPago = 0m
        };
        await _fixture.ReservaRepository.CriarAsync(reserva);

        // Act
        var totalReservas = await _fixture.ReservaRepository.ContarReservasPorEventoAsync(eventoId);
        var totalUsuario = await _fixture.ReservaRepository.ContarReservasUsuarioPorEventoAsync(usuario.Cpf, eventoId);

        // Assert
        Assert.True(totalReservas > 0);
        Assert.True(totalUsuario > 0);
    }

    [Fact]
    public async Task Reserva_ObterPorCodigoIngresso_DeveFuncionar()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var evento = CriarEventoTeste();
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var codigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20];
        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = evento.PrecoPadrao,
            DataCompra = DateTime.Now,
            CodigoIngresso = codigoIngresso,
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            TemSeguro = false,
            ValorSeguroPago = 0m
        };
        await _fixture.ReservaRepository.CriarAsync(reserva);

        // Act
        var obtida = await _fixture.ReservaRepository.ObterPorCodigoIngressoAsync(codigoIngresso);

        // Assert
        Assert.NotNull(obtida);
        Assert.Equal(codigoIngresso, obtida.CodigoIngresso);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FLUXO TRANSACIONAL — COMPRA COM CUPOM
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FluxoCompleto_CompraComCupom_DeveCalcularDesconto()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var evento = CriarEventoTeste();
        evento.PrecoPadrao = 200.00m;
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var cupom = CriarCupomTeste();
        cupom.PorcentagemDesconto = 20.00m;
        cupom.ValorMinimoRegra = 100.00m;
        await _fixture.CupomRepository.CriarAsync(cupom);

        // Act — compra com cupom
        var valorBruto = evento.PrecoPadrao; // 200.00
        var desconto = valorBruto * (cupom.PorcentagemDesconto / 100m); // 40.00
        var valorFinal = valorBruto - desconto; // 160.00

        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = valorFinal,
            CupomUtilizado = cupom.Codigo,
            TemSeguro = false,
            DataCompra = DateTime.Now,
            CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20],
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            ValorSeguroPago = 0m
        };

        await _fixture.ReservaRepository.CriarAsync(reserva);
        await _fixture.CupomRepository.IncrementarUsoAsync(cupom.Codigo);
        await _fixture.EventoRepository.DiminuirCapacidadeAsync(eventoId);

        // Assert
        var reservaObtida = await _fixture.ReservaRepository.ObterDetalhadaPorIdAsync(reserva.Id, usuario.Cpf);
        Assert.NotNull(reservaObtida);
        Assert.Equal(valorFinal, reservaObtida.ValorFinalPago);
        Assert.Equal(cupom.Codigo, reservaObtida.CupomUtilizado);

        var cupomObtido = await _fixture.CupomRepository.ObterPorCodigoAsync(cupom.Codigo);
        Assert.NotNull(cupomObtido);
        Assert.Equal(1, cupomObtido.TotalUsado);

        var eventoObtido = await _fixture.EventoRepository.ObterPorIdAsync(eventoId);
        Assert.NotNull(eventoObtido);
        Assert.Equal(evento.CapacidadeTotal - 1, eventoObtido.CapacidadeTotal);
    }

    [Fact]
    public async Task FluxoCompleto_CancelarCompra_DeveRestaurarCapacidade()
    {
        // Arrange
        var usuario = CriarUsuarioTeste();
        await _fixture.UsuarioRepository.CriarUsuario(usuario);

        var evento = CriarEventoTeste();
        var eventoId = await _fixture.EventoRepository.AdicionarAsync(evento);

        var reserva = new Reservation
        {
            EventoId = eventoId,
            UsuarioCpf = usuario.Cpf,
            ValorFinalPago = evento.PrecoPadrao,
            DataCompra = DateTime.Now,
            CodigoIngresso = Guid.NewGuid().ToString("N").ToUpper()[..20],
            Status = "Ativa",
            TaxaServicoPago = evento.TaxaServico,
            TemSeguro = false,
            ValorSeguroPago = 0m
        };
        var criada = await _fixture.ReservaRepository.CriarAsync(reserva);

        // "Compra" realizada — diminui capacidade
        await _fixture.EventoRepository.DiminuirCapacidadeAsync(eventoId);
        var capacidadeAposCompra = (await _fixture.EventoRepository.ObterPorIdAsync(eventoId))!.CapacidadeTotal;

        // Act — cancela a reserva
        await _fixture.ReservaRepository.CancelarAsync(criada.Id, usuario.Cpf);
        await _fixture.EventoRepository.AumentarCapacidadeAsync(eventoId);
        var capacidadeAposCancelamento = (await _fixture.EventoRepository.ObterPorIdAsync(eventoId))!.CapacidadeTotal;

        // Assert
        Assert.Equal(evento.CapacidadeTotal - 1, capacidadeAposCompra);
        Assert.Equal(evento.CapacidadeTotal, capacidadeAposCancelamento);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HEALTH CHECK — BANCO ACESSÍVEL
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Database_DeveEstarAcessivel()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            IntegrationTestFixture.ObterConnectionString());

        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var result = await cmd.ExecuteScalarAsync();

        Assert.NotNull(result);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Database_TabelaUsuarios_DeveExistir()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            IntegrationTestFixture.ObterConnectionString());
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM sys.tables 
            WHERE name = 'Usuarios' AND SCHEMA_NAME(schema_id) = 'dbo'";

        var count = (int)(await cmd.ExecuteScalarAsync())!;

        Assert.True(count > 0, "Tabela Usuarios não encontrada no banco.");
    }
}
