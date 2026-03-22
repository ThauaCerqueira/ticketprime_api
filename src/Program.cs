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

app.Run();
