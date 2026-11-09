using BusinessLogic.Auth.Implementations;
using DataAccess.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tests;

public class JwtTokenGeneratorTests
{
    private static Cliente CrearCliente() => new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Prueba",
        Apellido = "Usuario",
        Email = "prueba@test.com",
        Telefono = "123456789",
        Rol = "Cliente",
        Activo = true,
        FechaRegistro = DateTime.UtcNow
    };

    [Fact]
    public void GenerateToken_SinClaveConfiguradaNiEntorno_LanzaInvalidOperationException()
    {
        // Arrange — Sin Jwt:SecretKey ni variable de entorno
        var inMemorySettings = new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Aseguramos que la variable de entorno no interfiera
        var envSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        var envAgySecret = Environment.GetEnvironmentVariable("AGY_JWT_SECRET");
        try
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", null);
            Environment.SetEnvironmentVariable("AGY_JWT_SECRET", null);

            var sut = new JwtTokenGenerator(configuration);
            var cliente = CrearCliente();

            // Act & Assert — Debe fallar y NO usar fallback hardcodeado
            var ex = Assert.Throws<InvalidOperationException>(() => sut.GenerateToken(cliente));
            Assert.Contains("clave secreta", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", envSecret);
            Environment.SetEnvironmentVariable("AGY_JWT_SECRET", envAgySecret);
        }
    }

    [Fact]
    public void GenerateToken_ConClaveValida_GeneraTokenConExpiracionAproximada60Minutos()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:SecretKey", "ClaveSecretaSuperSeguraParaTestsDeUnitarias1234567890!" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" },
            { "Jwt:ExpirationMinutes", "60" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var sut = new JwtTokenGenerator(configuration);
        var cliente = CrearCliente();

        // Act
        var (token, expiration) = sut.GenerateToken(cliente);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        var diferenciaMinutos = (expiration - DateTime.UtcNow).TotalMinutes;
        Assert.True(diferenciaMinutos >= 58 && diferenciaMinutos <= 61,
            $"La expiración esperada era ~60 minutos, pero fue de {diferenciaMinutos:F2} minutos.");
    }

    [Fact]
    public void GenerateRefreshToken_GeneraTokenCriptograficoValidoYUnico()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:SecretKey", "ClaveSecretaSuperSeguraParaTestsDeUnitarias1234567890!" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var sut = new JwtTokenGenerator(configuration);

        // Act
        var refreshToken1 = sut.GenerateRefreshToken();
        var refreshToken2 = sut.GenerateRefreshToken();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(refreshToken1));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken2));
        Assert.NotEqual(refreshToken1, refreshToken2);

        // Verificar que sea Base64 válido
        var bytes = Convert.FromBase64String(refreshToken1);
        Assert.Equal(64, bytes.Length);
    }
}

