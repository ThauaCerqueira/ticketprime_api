using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Service;
using src.Models;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURAÇÃO DE CONEXÃO ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";

// --- 2. CONFIGURAÇÃO DO CORS (Obrigatório para Blazor) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --- 3. REGISTRO DE SERVIÇOS ---
builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer();

// Configura o HttpClient para a própria API conseguir se chamar (importante no SSR)
builder.Services.AddScoped(sp => new HttpClient { 
    BaseAddress = new Uri("https://localhost:5194") // Verifique se sua porta é a 7000 ou 5000
});

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<CupomRepository>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();
builder.Services.AddScoped<EventoService>();

var app = builder.Build();

// --- 4. MIDDLEWARES ---
app.UseHttpsRedirection();

// ATIVE O CORS AQUI
app.UseCors("AllowAll"); 

app.MapControllers();

// Rota de Usuários
app.MapPost("/api/usuarios", async (Usuario usuario, UsuarioRepository repository) =>
{
    var usuarioExistente = await repository.ObterPorCpf(usuario.Cpf);
    if (usuarioExistente != null)
    {
        return Results.BadRequest("Erro: O CPF informado já está cadastrado.");
    }
    await repository.CriarUsuario(usuario);
    return Results.Created($"/api/usuarios/{usuario.Cpf}", usuario);
});

app.Run();