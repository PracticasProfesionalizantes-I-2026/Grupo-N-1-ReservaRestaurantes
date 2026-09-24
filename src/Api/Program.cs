using BusinessLogic;
using DataAccess.Context;
using DataAccess.Data;
using DataAccess.Repositories.Implementations;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configuración de DbContext SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=restaurant.db";

builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options.UseSqlite(connectionString, b => b.MigrationsAssembly("Migrations")));

// Registro de Repositorios (DataAccess)
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IMesaRepository, MesaRepository>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();

// Registro de Servicios (BusinessLogic)
builder.Services.AddBusinessLogic();

// Controladores y documentación API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

// Inicialización y seed de la base de datos
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
    await DbInitializer.InitializeAsync(dbContext);
}

// Configuración del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
