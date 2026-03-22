using src.Infrastructure;
using src.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

builder.Services.AddScoped<UsuarioRepository>();

var app = builder.Build();

// --- ENDPOINTS ---

// Cadastro de Usuários com Critério 4 da AV1
app.MapPost("/api/usuarios", async (Usuario usuario, UsuarioRepository repository) =>
{
    // Verificar se o CPF já existe
    var usuarioExistente = await repository.ObterPorCpf(usuario.Cpf);

    if (usuarioExistente != null)
    {
        // Retorna erro 400 exigido pela avaliação
        return Results.BadRequest("Erro: O CPF informado já está cadastrado.");
    }

    // Cria o usuário se o CPF for novo
    await repository.CriarUsuario(usuario);
    
    return Results.Created($"/api/usuarios/{usuario.Id}", "Usuário criado com sucesso");
});

// Listagem de Usuários
app.MapGet("/api/usuarios", async (UsuarioRepository repository) =>
{
    var usuarios = await repository.ListarUsuarios();
    return Results.Ok(usuarios);
});

app.Run();
