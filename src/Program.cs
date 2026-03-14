using src.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<DbConnectionFactory>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    return new DbConnectionFactory(connectionString!);
});

builder.Services.AddScoped<UsuarioRepository>();

var app = builder.Build();

app.MapControllers();

app.MapGet("/", () => "Hello World!");

app.Run();
