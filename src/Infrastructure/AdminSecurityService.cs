using Dapper;
using Microsoft.Data.SqlClient;

namespace src.Infrastructure;

/// <summary>
/// Serviço de segurança para o usuário administrador.
/// Executa verificações críticas na inicialização da aplicação.
///
/// ═══════════════════════════════════════════════════════════════════
/// PROBLEMA:
///   O script.sql cria um admin padrão com senha conhecida (admin123).
///   Se essa senha não for trocada em produção, qualquer um pode
///   acessar o sistema como administrador.
///
/// SOLUÇÃO:
///   - Verifica na inicialização se o admin padrão ainda está com
///     a senha original.
///   - Marca o admin como "senha temporária" para forçar a troca
///     no primeiro login.
///   - Em produção, bloqueia o login se a senha não foi trocada
///     (configurável via appsettings).
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public sealed class AdminSecurityService
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSecurityService> _logger;

    // Hash BCrypt conhecido da senha "admin123" com work factor 11
    // ATUALIZAR este hash se o script.sql mudar a senha padrão
    private const string DefaultAdminPasswordHash =
        "$2a$11$5VkqHKVPfZOz9OPGaFnOaeCJ0FCFHjP4NBPQ2VqGpjqMRFJG5tY5q";

    private const string DefaultAdminCpf = "00000000191";

    public AdminSecurityService(
        IConfiguration configuration,
        ILogger<AdminSecurityService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    /// <summary>
    /// Verifica e, se necessário, força a troca de senha do admin padrão.
    /// Deve ser chamado na inicialização da aplicação.
    /// </summary>
    public async Task CheckAndForcePasswordChangeAsync()
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Verifica se o admin padrão existe e ainda está com a senha original
            var admin = await conn.QueryFirstOrDefaultAsync<AdminInfo>(
                "SELECT Cpf, Senha, Email, EmailVerificado FROM Usuarios WHERE Cpf = @Cpf AND Perfil = 'ADMIN'",
                new { Cpf = DefaultAdminCpf });

            if (admin == null)
            {
                _logger.LogInformation(
                    "Admin padrão ({Cpf}) não encontrado. " +
                    "Provavelmente foi removido ou renomeado. Nenhuma ação necessária.",
                    DefaultAdminCpf);
                return;
            }

            // Verifica se a senha atual é a padrão
            var isDefaultPassword = BCrypt.Net.BCrypt.Verify("admin123", admin.Senha);

            if (isDefaultPassword)
            {
                // ═══════════════════════════════════════════════════════════════
                // GERA UMA NOVA SENHA ALEATÓRIA AUTOMATICAMENTE!
                //
                // Ao invés de apenas alertar, o sistema gera uma senha de 16
                // caracteres alfanuméricos, atualiza o banco e LOGA a nova
                // senha no console. O administrador deve copiá-la dos logs.
                //
                // A senha antiga ('admin123') NUNCA mais funcionará.
                // ═══════════════════════════════════════════════════════════════
                var novaSenha = GerarSenhaAleatoria(16);
                var novoHash = BCrypt.Net.BCrypt.HashPassword(novaSenha,
                    workFactor: _configuration.GetValue<int>("ConfiguracaoBCrypt:WorkFactor", 11));

                await conn.ExecuteAsync(
                    "UPDATE Usuarios SET Senha = @Senha, SenhaTemporaria = 1 WHERE Cpf = @Cpf",
                    new { Senha = novoHash, Cpf = DefaultAdminCpf });

                // ═══════════════════════════════════════════════════════════════
                // A senha é exibida APENAS no console (stdout) para que o
                // administrador a copie. NUNCA logamos a senha em texto puro
                // via ILogger — logs podem ser capturados por agregadores
                // (Datadog, Splunk, CloudWatch) ou expostos em dashboards.
                // ═══════════════════════════════════════════════════════════════
                var senhaMascarada = $"{novaSenha[..3]}***{novaSenha[^3..]}";

                var msg = $@"
╔══════════════════════════════════════════════════════════════╗
║        🔐 NOVA SENHA DO ADMINISTRADOR GERADA              ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║  CPF:     {DefaultAdminCpf}                                         ║
║  SENHA:   {novaSenha}                         ║
║                                                              ║
║  📋 COPIE A SENHA ACIMA — ela só será exibida esta vez!     ║
║                                                              ║
║  ⚠  Troque esta senha no primeiro login.                   ║
║  ⚠  Esta mensagem não se repete em reinicializações.       ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝";

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(msg);
                Console.ResetColor();

                // ═══════════════════════════════════════════════════════════════
                // Logamos APENAS a senha mascarada — mostra os primeiros 3 e
                // últimos 3 caracteres, o suficiente para o admin confirmar
                // que foi gerada, sem expor o valor completo.
                // ═══════════════════════════════════════════════════════════════
                _logger.LogWarning(
                    "Nova senha do admin gerada automaticamente. CPF={Cpf}, Senha (mascarada)={SenhaMask}",
                    DefaultAdminCpf, senhaMascarada);

                // Marca email como verificado
                if (admin.EmailVerificado == false || admin.EmailVerificado == null)
                {
                    await conn.ExecuteAsync(
                        "UPDATE Usuarios SET EmailVerificado = 1 WHERE Cpf = @Cpf",
                        new { Cpf = DefaultAdminCpf });
                }
            }
            else
            {
                _logger.LogInformation(
                    "Admin padrão ({Cpf}) já trocou a senha. Segurança OK.", DefaultAdminCpf);
            }

            // Verifica work factor do BCrypt
            var currentHash = admin.Senha;
            var interrogated = BCrypt.Net.BCrypt.InterrogateHash(currentHash);
            var workFactor = int.Parse(interrogated.WorkFactor?.ToString() ?? "0");
            var recommendedWorkFactor = _configuration.GetValue<int>("ConfiguracaoBCrypt:WorkFactor", 11);

            if (workFactor < recommendedWorkFactor)
            {
                _logger.LogWarning(
                    "Work factor do BCrypt para admin ({Cpf}) é {Current}, " +
                    "recomendado é {Recommended}. Considere re-hash da senha.",
                    DefaultAdminCpf, workFactor, recommendedWorkFactor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao verificar segurança do admin padrão.");
        }
    }

    private sealed class AdminInfo
    {
        public string Cpf { get; set; } = "";
        public string Senha { get; set; } = "";
        public string? Email { get; set; }
        public bool? EmailVerificado { get; set; }
    }

    /// <summary>
    /// Gera uma senha aleatória segura com caracteres alfanuméricos e especiais.
    /// Usa RandomNumberGenerator (criptograficamente seguro) em vez de Random.
    /// </summary>
    private static string GerarSenhaAleatoria(int tamanho)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%&*";
        var bytes = new byte[tamanho];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

/// <summary>
/// Middleware que verifica se o admin está com senha temporária
/// e força a troca antes de permitir qualquer ação administrativa.
/// </summary>
public sealed class AdminPasswordChangeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminPasswordChangeMiddleware> _logger;

    // Hash BCrypt conhecido da senha padrão
    private const string DefaultAdminPasswordHash =
        "$2a$11$5VkqHKVPfZOz9OPGaFnOaeCJ0FCFHjP4NBPQ2VqGpjqMRFJG5tY5q";

    private static bool? _adminHasDefaultPassword;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly object CheckLock = new();

    public AdminPasswordChangeMiddleware(RequestDelegate next, ILogger<AdminPasswordChangeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Só bloqueia em produção
        // Só bloqueia admins tentando acessar endpoints administrativos
        if (context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsProduction()
            && context.User?.Identity?.IsAuthenticated == true
            && context.User.IsInRole("ADMIN")
            && IsAdminEndpoint(context.Request.Path))
        {
            // Verifica se admin ainda tem senha padrão (com cache)
            var isDefault = CheckDefaultPasswordCached(context);

            if (isDefault == true)
            {
                // Se a rota NÃO é de troca de senha, bloqueia
                if (!context.Request.Path.StartsWithSegments("/api/auth/trocar-senha", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    var msg = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        mensagem = "A senha padrão do administrador precisa ser alterada. " +
                                   "Faça login e troque a senha em /trocar-senha antes de continuar.",
                        codigo = "ADMIN_PASSWORD_CHANGE_REQUIRED"
                    });
                    await context.Response.WriteAsync(msg);
                    return;
                }
            }
        }

        await _next(context);
    }

    private static bool? CheckDefaultPasswordCached(HttpContext context)
    {
        if ((DateTime.UtcNow - _lastCheck) < CheckInterval && _adminHasDefaultPassword.HasValue)
            return _adminHasDefaultPassword;

        lock (CheckLock)
        {
            if ((DateTime.UtcNow - _lastCheck) < CheckInterval && _adminHasDefaultPassword.HasValue)
                return _adminHasDefaultPassword;

            var logger = context.RequestServices
                .GetService<ILogger<AdminPasswordChangeMiddleware>>();
            try
            {
                var dbFactory = context.RequestServices.GetRequiredService<DbConnectionFactory>();
                using var conn = dbFactory.CreateConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Senha FROM Usuarios WHERE Cpf = '00000000191' AND Perfil = 'ADMIN'";
                var senha = cmd.ExecuteScalar() as string;

                _adminHasDefaultPassword = senha != null
                    && BCrypt.Net.BCrypt.Verify("admin123", senha);
            }
            catch (Exception ex)
            {
                // ═══════════════════════════════════════════════════════════════
                // SEGURANÇA: fail-safe em produção.
                //   Se não for possível verificar a senha do admin no banco
                //   (ex: banco fora do ar), BLOQUEAMOS o endpoint administrativo.
                //   Isso impede que um admin com senha padrão acesse o sistema
                //   aproveitando uma falha de conectividade.
                //   Fora de produção, deixamos passar para não travar o dev.
                // ═══════════════════════════════════════════════════════════════
                var isProd = context.RequestServices
                    .GetRequiredService<IWebHostEnvironment>().IsProduction();

                logger?.LogError(ex,
                    "Falha ao verificar senha do admin no banco. " +
                    "Comportamento: {Behavior}",
                    isProd ? "BLOQUEADO (produção, fail-safe)" : "PERMITIDO (não-produção, fail-open)");

                // Em produção: bloqueia (true = tem senha padrão = bloqueia acesso)
                // Fora de produção: permite (null = indeterminado = deixa passar)
                _adminHasDefaultPassword = isProd ? true : null;
            }

            _lastCheck = DateTime.UtcNow;
            return _adminHasDefaultPassword;
        }
    }

    private static bool IsAdminEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/eventos", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/cupons", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/meia-entrada", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/ingressos", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/fila-espera/evento", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
    }
}
