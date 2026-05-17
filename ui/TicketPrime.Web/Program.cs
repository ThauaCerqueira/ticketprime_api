using MudBlazor.Services;
using TicketPrime.Web.Client.Services;
using TicketPrime.Web.Components;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();

// Serviços compartilhados com o Client (necessários para pré-renderização)
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<CryptoService>();
builder.Services.AddSingleton<HealthCheckService>();
builder.Services.AddScoped<AuthHttpClientHandler>();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5164";
builder.Services.AddHttpClient("TicketPrimeApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    options.CircuitBreaker.MinimumThroughput = 5;
});

// FluentValidation (mesmos validators do Client, necessários para pré-renderização)
builder.Services.AddValidatorsFromAssemblyContaining<TicketPrime.Web.Client.Validators.RegistrationRequestValidator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TicketPrime.Web.Client._Imports).Assembly);

app.Run();
