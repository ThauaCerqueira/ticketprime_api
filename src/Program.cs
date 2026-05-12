using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Service;

namespace src;

public class Program
{
    private static ILogger<Program>? _logger;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ════════════════════════════════════════════════════════════
        //  Structured logging via built-in JsonConsole provider
        // ════════════════════════════════════════════════════════════
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";
        });

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' não encontrada. Configure em appsettings.json ou User Secrets.");

        // Initialize database
        InitializeDatabase(connectionString);

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:5194" };
                policy.WithOrigins(origins)
                      .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                      .WithHeaders("Content-Type", "Authorization", "Accept", "X-Request-Id");
            });
        });

        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Chave JWT 'Jwt:Key' não encontrada. Configure via variável de ambiente 'Jwt__Key', " +
                "User Secrets (Development) ou o arquivo appsettings.json. " +
                "Use uma chave de no mínimo 32 caracteres (256 bits) para HMAC-SHA256.");

        if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            throw new InvalidOperationException(
                "A chave JWT ('Jwt:Key') deve ter no mínimo 32 caracteres (256 bits) para HMAC-SHA256. " +
                "Configure-a via variável de ambiente 'Jwt__Key', User Secrets (dotnet user-secrets) " +
                "ou o arquivo appsettings.Development.json (que é ignorado pelo git). " +
                "NUNCA utilize a chave padrão do repositório em produção.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TicketPrime",
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TicketPrime",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                // Suporta token vindo de cookie httpOnly (ticketprime_token) como fallback
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Cookies["ticketprime_token"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        // ── CSRF ────────────────────────────────────────────────────────────
        // Nota: Não usamos UseAntiforgery() global porque a API usa JWT Bearer
        // Authentication (via header Authorization: Bearer <token>) e cookie
        // httpOnly com SameSite=Strict, que são inerentemente imunes a CSRF.
        // O AddAntiforgery fica registrado para uso opcional em endpoints
        // específicos que possam usar autenticação baseada em cookie puro.
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.SuppressXFrameOptionsHeader = false;
        });

        // ── Rate Limiters com particionamento por usuário ───────────────────
        //
        // ANTES (problemático): FixedWindowLimiter particionava apenas por IP.
        //   Um atacante com botnet de múltiplos IPs conseguia fazer scalping
        //   de ingressos sem limitação efetiva por conta.
        //
        // AGORA (corrigido): Usamos AddPerUserPolicy que particiona por:
        //   - Usuário autenticado: chave = "user_{CPF}" (via ClaimTypes.NameIdentifier)
        //   - Anônimo: chave = "ip_{endereço}" (fallback)
        //   - Admin: limite mais alto (não afeta operações administrativas)
        //
        // Isso garante que mesmo com múltiplos IPs, cada conta fica
        // limitada individualmente.
        builder.Services.AddRateLimiter(options =>
        {
            // Login: 5 tentativas/minuto (por IP — endpoint não autenticado)
            options.AddFixedWindowLimiter("login", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 5;
                o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            // Compra de ingressos: 3/min por usuário, 1/min anônimo, 30/min admin
            // Policy per-user — protege contra scalping via botnet
            options.AddPerUserCompraPolicy("compra-ingresso");

            // Escrita (criação de eventos, cupons): 10/min por usuário, 100/min admin
            // Policy per-user — evita abuso mesmo com IPs diferentes
            options.AddPerUserPolicy("escrita",
                anonymousLimit: 3,
                authenticatedLimit: 10,
                adminLimit: 100,
                window: TimeSpan.FromMinutes(1));

            // Geral (leituras, listagens): 60/min por usuário, 300/min admin
            // Policy per-user — distribui carga entre contas genuínas
            options.AddPerUserPolicy("geral",
                anonymousLimit: 30,
                authenticatedLimit: 60,
                adminLimit: 300,
                window: TimeSpan.FromMinutes(1));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        // ════════════════════════════════════════════════════════════
        //  Swagger / OpenAPI — documentação disponível em todos os
        //  ambientes para facilitar debugging e integração.
        // ════════════════════════════════════════════════════════════
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            // Define metadata da API
            options.SwaggerDoc("v1", new()
            {
                Title = "TicketPrime API",
                Version = "v1",
                Description = "API de gerenciamento de eventos e ingressos da TicketPrime. " +
                              "Fornece endpoints para autenticação, criação de eventos, " +
                              "compra de ingressos, gerenciamento de cupons e muito mais."
            });

            // Inclui XML comments dos controllers/endpoints (se existirem)
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        // ── DI Registrations ───────────────────────────────────────────────
        builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
        builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        builder.Services.AddScoped<ICupomRepository, CupomRepository>();
        builder.Services.AddScoped<IEventoRepository, EventoRepository>();
        builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
        builder.Services.AddScoped<IFilaEsperaRepository, FilaEsperaRepository>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<CouponService>();
        builder.Services.AddScoped<EventService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<ITransacaoCompraExecutor, TransacaoCompraExecutor>();
        builder.Services.AddScoped<ReservationService>();
        builder.Services.AddScoped<IWaitingQueueService, WaitingQueueService>();
        builder.Services.AddSingleton<CryptoKeyService>();
        builder.Services.AddSingleton<MetricsService>();
        builder.Services.AddHostedService<RefreshTokenCleanupService>();

        // ── Gateway de pagamento ────────────────────────────────────────────
        var mpToken = builder.Configuration["MercadoPago:AccessToken"];
        if (!string.IsNullOrWhiteSpace(mpToken))
        {
            builder.Services.AddHttpClient("MercadoPago", c =>
            {
                c.BaseAddress = new Uri("https://api.mercadopago.com/");
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", mpToken);
            });
            builder.Services.AddScoped<IPaymentGateway, MercadoPagoPaymentGateway>();
        }
        else
        {
            builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
        }

        // ── Armazenamento de arquivos ───────────────────────────────────────
        builder.Services.AddScoped<IStorageService, LocalFileStorageService>();

        // ── Meia-entrada (Lei 12.933/2013) ─────────────────────────────────
        builder.Services.AddScoped<IMeiaEntradaRepository, MeiaEntradaRepository>();
        builder.Services.AddScoped<IMeiaEntradaStorageService, LocalMeiaEntradaStorageService>();

        // ── Audit Log (Financial Audit Trail) ──────────────────────────────
        builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        builder.Services.AddScoped<AuditLogService>();

        // ── Avaliações ──────────────────────────────────────────────────────
        builder.Services.AddScoped<IAvaliacaoRepository, AvaliacaoRepository>();
        builder.Services.AddScoped<AvaliacaoService>();

        // Email template service para emails transacionais (depende de IEmailService)
        builder.Services.AddSingleton<EmailTemplateService>();

        // Email service: usa SmtpEmailService se configurado, senão ConsoleEmailService (dev)
        var smtpHost = builder.Configuration["EmailSettings:SmtpHost"];
        if (!string.IsNullOrEmpty(smtpHost))
        {
            builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
        }
        else
        {
            builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
        }

        // ── Controllers ────────────────────────────────────────────────────
        builder.Services.AddControllers();

        var app = builder.Build();

        // Logger disponível a partir daqui para logs estruturados
        _logger = app.Services.GetRequiredService<ILogger<Program>>();

        // ════════════════════════════════════════════════════════════
        //  Swagger — apenas em Development e Staging.
        //  Em produção (ASPNETCORE_ENVIRONMENT=Production) o endpoint
        //  /swagger não é exposto para evitar vazamento de contratos.
        // ════════════════════════════════════════════════════════════
        if (!app.Environment.IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "TicketPrime API v1");
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "TicketPrime API — Documentação Swagger";
            });
        }

        // Startup diagnostics via ILogger estruturado
        _logger.LogInformation(
            "PaymentGateway: {Gateway}",
            mpToken is { Length: > 0 } ? "MercadoPago (produção)" : "SimulatedPaymentGateway (desenvolvimento)");
        _logger.LogInformation(
            "EmailService: {Service}",
            smtpHost is { Length: > 0 } ? "SmtpEmailService (SMTP configurado)" : "ConsoleEmailService (modo dev — emails exibidos no console)");

        // HTTPS enforcement
        app.UseHttpsRedirection();
        app.UseHsts();

        // ── Security Headers ────────────────────────────────────────────────
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"]        = "DENY";
            ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
            ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
            ctx.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self'; frame-ancestors 'none';";
            await next();
        });

        app.UseCors("AllowFrontend");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        // ── Metrics Middleware ──────────────────────────────────────────────
        // Registra métricas de todas as requisições HTTP (endpoint, status, duração)
        app.Use(async (context, next) =>
        {
            var metrics = context.RequestServices.GetRequiredService<MetricsService>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                sw.Stop();
                metrics.RecordRequest(
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedTicks);
            }
        });

        // ── Controllers ────────────────────────────────────────────────────
        app.MapControllers();

        app.Run();
    }

    private static void InitializeDatabase(string connectionString)
    {
        // Antes do builder.Build(), usamos Console.Write como fallback
        // (ILogger ainda não está disponível)
        // Try multiple candidate paths — the relative ../db path works for local dev
        // (cwd = src/), but breaks in Docker where WORKDIR is /app and db/ is a sibling.
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "db", "script.sql"),              // Docker: /app/db/script.sql
            Path.Combine(AppContext.BaseDirectory, "..", "db", "script.sql"),        // fallback
            Path.Combine(Directory.GetCurrentDirectory(), "db", "script.sql"),       // cwd/db
            Path.Combine(Directory.GetCurrentDirectory(), "..", "db", "script.sql"), // local dev (src/ -> db/)
        };

        var scriptPath = candidatePaths.Select(Path.GetFullPath).FirstOrDefault(File.Exists);

        Console.WriteLine($"[DB Init] Looking for database script...");
        foreach (var p in candidatePaths.Select(Path.GetFullPath))
            Console.WriteLine($"[DB Init]   {(File.Exists(p) ? "FOUND" : "     ")} {p}");

        if (scriptPath == null)
        {
            Console.WriteLine("[DB Init] Warning: Database script not found in any candidate path. Skipping init.");
            return;
        }

        Console.WriteLine($"[DB Init] Using: {scriptPath}");

        try
        {
            var sqlScript = File.ReadAllText(scriptPath);

            // Split on lines that contain only "GO" (case-insensitive, with optional whitespace/semicolons).
            // This is far more robust than hardcoding newline variants and catches edge cases like
            // trailing whitespace, mixed line endings, or blank lines between GO and the next statement.
            var statements = Regex.Split(
                sqlScript,
                @"^\s*GO\s*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            // Change connection string to use master database for initial setup
            var masterConnectionString = connectionString.Replace("Database=TicketPrime", "Database=master");

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(masterConnectionString);
            connection.Open();
            Console.WriteLine("[DB Init] ✓ Connected to SQL Server for database initialization");

            foreach (var statement in statements)
            {
                var trimmedStatement = statement.Trim();
                if (trimmedStatement.Length == 0)
                    continue;

                // ── Skip standalone "GO" statements that the regex split didn't catch ──
                // Edge cases like trailing "GO" at end-of-file or "GO" inside dynamic SQL
                // can leave a stray "GO" token. Sending it to SQL Server would cause:
                //   "Could not find stored procedure 'GO'".
                if (string.Equals(trimmedStatement, "GO", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmedStatement.TrimEnd(';'), "GO", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var command = connection.CreateCommand();
                command.CommandText = trimmedStatement;
                command.CommandTimeout = 60;

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DB Init] ⚠ Database setup notice: {ex.Message}");
                }
            }

            connection.Close();
            Console.WriteLine("[DB Init] ✓ Database initialization completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB Init] ⚠ Warning during database setup: {ex.Message}");
        }
    }
}
