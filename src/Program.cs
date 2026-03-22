using src.Infrastructure;
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