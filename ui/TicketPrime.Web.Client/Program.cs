using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http.Resilience;
using MudBlazor.Services;
using TicketPrime.Web.Client;
using TicketPrime.Web.Client.Services;
using TicketPrime.Web.Client.Validators;
using FluentValidation;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// MudBlazor
builder.Services.AddMudServices();

// Session (Scoped)
builder.Services.AddScoped<SessionService>();

// CryptoService
builder.Services.AddScoped<CryptoService>();

// HealthCheckService
builder.Services.AddSingleton<HealthCheckService>();

// HTTP Client for API calls with JWT authentication and retry policy
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped<AuthHttpClientHandler>();

// "TicketPrimeApi" — HttpClient com Polly + AuthHttpMessageHandler
// NOTA: AddHttpMessageHandler NÃO pode ser encadeado após AddStandardResilienceHandler
// porque ele retorna IHttpStandardResiliencePipelineBuilder, não IHttpClientBuilder.
// A abordagem correta para WASM é usar IHttpClientFactory + criar o HttpClient manualmente.
builder.Services.AddHttpClient("TicketPrimeApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(400);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.Retry.ShouldHandle = static args =>
    {
        if (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return ValueTask.FromResult(false);
        if (args.Outcome.Exception != null)
            return ValueTask.FromResult(true);
        return ValueTask.FromResult(false);
    };
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    options.CircuitBreaker.MinimumThroughput = 5;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(25);
});

// Cria o HttpClient com AuthHttpClientHandler resolvido manualmente
builder.Services.AddScoped(sp =>
{
    var sessionService = sp.GetRequiredService<SessionService>();
    var authHandler = new AuthHttpClientHandler(sessionService)
    {
        InnerHandler = sp.GetRequiredService<IHttpMessageHandlerFactory>()
                          .CreateHandler("TicketPrimeApi")
    };
    return new HttpClient(authHandler, disposeHandler: false)
    {
        BaseAddress = new Uri(apiBaseUrl),
        Timeout = TimeSpan.FromSeconds(30)
    };
});

// CupomService
builder.Services.AddScoped(sp =>
{
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var http = httpFactory.CreateClient("TicketPrimeApi");
    return new CupomService(http);
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

await builder.Build().RunAsync();
