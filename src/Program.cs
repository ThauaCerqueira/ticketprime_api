using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Service;
using src.Models;

var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";


builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<CupomRepository>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();


builder.Services.AddScoped<EventoService>();

var app = builder.Build();


app.UseHttpsRedirection();
app.UseAuthorization();


app.MapGet("/", () => "TicketPrime API está rodando!");


app.MapControllers();


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