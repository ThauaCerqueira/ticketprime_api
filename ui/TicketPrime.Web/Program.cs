using TicketPrime.Web.Components;
using TicketPrime.Web.Services;
using src.Service;
using src.Infrastructure;
using src.Infrastructure.Repository;
using src.Infrastructure.IRepository;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor (componentes visuais + serviços de Snackbar, Dialog, etc.)
builder.Services.AddMudServices();

// Serviço de criptografia E2E (wraps Web Crypto API via JSInterop)
builder.Services.AddScoped<CryptoService>();

// Infrastructure
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
builder.Services.AddScoped<ICupomRepository, CupomRepository>();

// Services
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EventoService>();
builder.Services.AddScoped<ReservaService>();
builder.Services.AddScoped<CupomService>();

// Session (Scoped = cada usuário tem sua própria sessão no InteractiveServer)
builder.Services.AddScoped<SessionService>();

// HttpClient for API calls with JWT authentication
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddScoped<AuthHttpClientHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthHttpClientHandler>();
    var client = new HttpClient(handler);
    client.BaseAddress = new Uri(apiBaseUrl);
    return client;
});

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
