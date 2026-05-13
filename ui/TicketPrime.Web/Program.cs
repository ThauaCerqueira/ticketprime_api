using FluentValidation;
using Microsoft.Extensions.Http.Resilience;
using TicketPrime.Web.Components;
using TicketPrime.Web.Models;
using TicketPrime.Web.Services;
using TicketPrime.Web.Validators;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════
// ⚠️  BLAZOR SERVER — LIMITAÇÃO SIGNALR
// ═══════════════════════════════════════════════════════════════════
// Este projeto usa Blazor Server (InteractiveServer), que mantém uma
// conexão SignalR persistente POR USUÁRIO. Isso implica:
//
//   1. Cada usuário aberto no navegador consome uma conexão WebSocket
//      no servidor. Acima de ~5.000 conexões simultâneas, pode ser
//      necessário escalar horizontalmente (Azure SignalR Service,
//      Redis backplane, sticky sessions).
//   2. A latência da UI depende da qualidade da rede (cada ação passa
//      pelo servidor).
//   3. Não há fallback para WebAssembly — se o SignalR cair, a UI para.
//
// ✅ PARA PRODUÇÃO (alta escala → 10k+ usuários simultâneos):
//    Migre para Blazor WebAssembly (WASM) OU use Auto (InteractiveAuto)
//    com um projeto WASM separado. O Auto render mode permite servir
//    as primeiras interações via Server (rápido) e depois baixar o WASM
//    em background para continuar no cliente.
//
//    Passos para migrar para InteractiveAuto:
//    1. Crie um projeto Blazor WebAssembly (ex: TicketPrime.Web.Client)
//    2. Mova Components/Pages e Components/Layout para o client
//    3. No server, adicione .AddInteractiveWebAssemblyComponents()
//    4. Use @rendermode InteractiveAuto nas páginas críticas
//    5. Compartilhe Models/DTOs como class library (TicketPrime.Web.Shared)
//
// 📚  https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models
// ═══════════════════════════════════════════════════════════════════

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // ⏱ CircuitOptions — controla comportamento de reconexão
        options.DetailedErrors = builder.Environment.IsDevelopment();
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    })
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 20 * 1024 * 1024; // 20 MB
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    });

// MudBlazor
builder.Services.AddMudServices();

// CryptoService (E2E encryption via Web Crypto API / JSInterop)
builder.Services.AddScoped<CryptoService>();

// Session (Scoped = each user has their own session in InteractiveServer)
builder.Services.AddScoped<SessionService>();

// HealthCheckService — monitora periodicamente a saúde da API backend
builder.Services.AddSingleton<HealthCheckService>();

// HTTP Client for API calls with JWT authentication and retry policy
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped<AuthHttpClientHandler>();
builder.Services.AddHttpClient("TicketPrimeApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthHttpClientHandler>()
.AddStandardResilienceHandler(options =>
{
    // ════════════════════════════════════════════════════════════
    //  RETRY POLICY — tentativas com backoff exponencial
    //  429 (TooManyRequests) NÃO é retentado: re-tentar esgota
    //  imediatamente a cota e dispara o circuit breaker.
    // ════════════════════════════════════════════════════════════
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(400);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true; // evita thundering herd
    options.Retry.ShouldHandle = static args =>
    {
        if (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return ValueTask.FromResult(false); // não retentar 429
        if (args.Outcome.Exception != null)
            return ValueTask.FromResult(true);
        var code = (int?)args.Outcome.Result?.StatusCode;
        return ValueTask.FromResult(code >= 500 || code == 408);
    };

    // ════════════════════════════════════════════════════════════
    //  CIRCUIT BREAKER — desarma rapidamente se API estiver fora
    //  429 não conta como falha: é throttling intencional, não
    //  indisponibilidade de serviço.
    // ════════════════════════════════════════════════════════════
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 8;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    options.CircuitBreaker.ShouldHandle = static args =>
    {
        if (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return ValueTask.FromResult(false); // 429 não dispara o circuit breaker
        if (args.Outcome.Exception != null)
            return ValueTask.FromResult(true);
        var code = (int?)args.Outcome.Result?.StatusCode;
        return ValueTask.FromResult(code >= 500 || code == 408);
    };

    // ════════════════════════════════════════════════════════════
    //  TIMEOUT — limite total por requisição
    // ════════════════════════════════════════════════════════════
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("TicketPrimeApi"));

// CupomService — serviço frontend que chama API REST de cupons
builder.Services.AddScoped<CupomService>();

// FluentValidation — registra validadores para injeção nas páginas
builder.Services.AddScoped<IValidator<EventoCreateDto>, EventoCreateDtoValidator>();
builder.Services.AddScoped<IValidator<RegistrationRequest>, RegistrationRequestValidator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// ═══════════════════════════════════════════════════════════════
//  Inicia o monitoramento de saúde da API após a aplicação estar
//  pronta para atender requisições.
// ═══════════════════════════════════════════════════════════════
var healthCheck = app.Services.GetRequiredService<HealthCheckService>();
healthCheck.IniciarMonitoramento();
