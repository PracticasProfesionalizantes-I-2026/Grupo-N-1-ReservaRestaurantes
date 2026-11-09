using System.Text;
using System.Threading.RateLimiting;
using Api.Middleware;
using BusinessLogic;
using DataAccess.Context;
using DataAccess.Data;
using DataAccess.Repositories.Implementations;
using DataAccess.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configuración de DbContext SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=restaurant.db";

builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options.UseSqlite(connectionString, b => b.MigrationsAssembly("Migrations"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Registro de Repositorios (DataAccess)
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IMesaRepository, MesaRepository>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();

// Registro de Servicios (BusinessLogic)
builder.Services.AddBusinessLogic();

// Configuración de Autenticación JWT Bearer (CU-05)
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] 
                   ?? Environment.GetEnvironmentVariable("JWT_SECRET") 
                   ?? Environment.GetEnvironmentVariable("AGY_JWT_SECRET");
if (string.IsNullOrWhiteSpace(jwtSecretKey))
{
    throw new InvalidOperationException("La clave secreta de JWT no está configurada. Configure 'Jwt:SecretKey' en User Secrets o mediante la variable de entorno 'JWT_SECRET'.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ReservaRestaurantesApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ReservaRestaurantesUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var revocationService = context.HttpContext.RequestServices.GetRequiredService<BusinessLogic.Auth.Interfaces.ITokenRevocationService>();
            var authHeader = context.Request.Headers.Authorization.ToString();
            var rawToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader[7..].Trim()
                : authHeader.Trim();

            var userIdStr = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            Guid? userId = Guid.TryParse(userIdStr, out var parsedId) ? parsedId : null;
            var iat = context.SecurityToken?.ValidFrom;

            if (revocationService.IsRevoked(rawToken, userId, iat))
            {
                context.Fail("El token ha sido revocado.");
            }

            return Task.CompletedTask;
        }
    };
});

// Manejo centralizado de excepciones (Middleware global)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Configuración de Rate Limiting para endpoints públicos (ej. registro de clientes)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("RegistrationPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

// Controladores y documentación API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenAPI con esquema de seguridad Bearer JWT para Scalar
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Ingresá el token JWT obtenido del endpoint /api/v1/Auth/login"
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });

        return Task.CompletedTask;
    });
});

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
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Reserva Restaurantes API")
               .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl)
               .AddPreferredSecuritySchemes("Bearer")
               .AddHttpAuthentication("Bearer", auth =>
               {
                   auth.Token = string.Empty;
               });
    });
}

// Middleware global de excepciones — debe ir antes de cualquier middleware que pueda fallar
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
