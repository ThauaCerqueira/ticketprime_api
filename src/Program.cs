using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Service;
{
    
}

var builder = WebApplication.CreateBuilder(args);

// 1. Adiciona suporte aos Controllers
builder.Services.AddControllers();

// 2. Registro da Factory (Mudado para Singleton para melhor performance)
builder.Services.AddSingleton<DbConnectionFactory>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new Exception("String de conexão 'DefaultConnection' não encontrada no appsettings.json");
    }
    return new DbConnectionFactory(connectionString);
});

// 3. Registro dos Repositories e Services
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();
builder.Services.AddScoped<EventoService>();


var app = builder.Build();


app.UseHttpsRedirection();

app.MapControllers();

app.MapGet("/", () => "TicketPrime API está rodando!");
using src.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                        ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<CupomRepository>();

var app = builder.Build();

// CADASTRO DE USUÁRIOS 
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