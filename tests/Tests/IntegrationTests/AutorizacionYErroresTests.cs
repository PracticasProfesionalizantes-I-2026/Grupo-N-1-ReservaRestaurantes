using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.DTOs.Auth;
using Shared.DTOs.Reservas;
using Xunit;

namespace Tests.IntegrationTests;

/// <summary>
/// Tests de integración de autorización (403/401) y de recursos inexistentes (404)
/// para los endpoints de gestión de reservas. Cubren las filas de las matrices de
/// CU-07, CU-10 a CU-16, CU-18 y CU-09.
/// </summary>
public class AutorizacionYErroresTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AutorizacionYErroresTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> TokenAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login",
            new LoginRequestDTO { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(JsonOptions);
        return result!.Token!;
    }

    private async Task AsClienteAsync()
    {
        var token = await TokenAsync("juan.perez@example.com", "Cliente123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task AsGerenteAsync()
    {
        var token = await TokenAsync("gerente@restaurant.com", "Gerente123!");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void Limpiar() => _client.DefaultRequestHeaders.Authorization = null;

    // ----- 401: sin token -----

    [Fact]
    public async Task GetContextoInicio_WithoutToken_Returns401Unauthorized()
    {
        Limpiar();
        var response = await _client.GetAsync("/api/v1/usuario/contexto-inicio");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ----- 403: cliente intentando acciones de gerente -----

    [Theory]
    [InlineData("PATCH", "/api/v1/Reservas/{0}/iniciar")]
    [InlineData("PATCH", "/api/v1/Reservas/{0}/finalizar")]
    [InlineData("PATCH", "/api/v1/Reservas/{0}/confirmar")]
    [InlineData("PATCH", "/api/v1/Reservas/gerente/{0}/cancelar")]
    [InlineData("GET", "/api/v1/Reservas/lista-espera")]
    [InlineData("GET", "/api/v1/Reservas/lista-reservas")]
    public async Task AccionesDeGerente_ComoCliente_Returns403Forbidden(string metodo, string ruta)
    {
        await AsClienteAsync();
        var url = string.Format(ruta, Guid.NewGuid());
        var request = new HttpRequestMessage(new HttpMethod(metodo), url)
        {
            Content = JsonContent.Create(new ReservaCancelarDTO { Motivo = "prueba" })
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task RechazarReserva_ComoCliente_Returns403Forbidden()
    {
        await AsClienteAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/Reservas/{Guid.NewGuid()}/rechazar",
            new ReservaRechazarDTO { MotivoRechazo = "prueba" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task PromoverListaEspera_ComoCliente_Returns403Forbidden()
    {
        await AsClienteAsync();
        var response = await _client.PatchAsync(
            $"/api/v1/Reservas/lista-espera/{Guid.NewGuid()}/promover", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    // ----- 404: recursos inexistentes (como gerente) -----

    [Theory]
    [InlineData("PATCH", "/api/v1/Reservas/{0}/iniciar")]
    [InlineData("PATCH", "/api/v1/Reservas/{0}/finalizar")]
    [InlineData("PATCH", "/api/v1/Reservas/{0}/confirmar")]
    public async Task AccionesSobreReservaInexistente_ComoGerente_Returns404NotFound(string metodo, string ruta)
    {
        await AsGerenteAsync();
        var url = string.Format(ruta, Guid.NewGuid());
        var request = new HttpRequestMessage(new HttpMethod(metodo), url);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task RechazarReservaInexistente_ComoGerente_Returns404NotFound()
    {
        await AsGerenteAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/Reservas/{Guid.NewGuid()}/rechazar",
            new ReservaRechazarDTO { MotivoRechazo = "prueba" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CancelarReservaGerenteInexistente_ComoGerente_Returns404NotFound()
    {
        await AsGerenteAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/Reservas/gerente/{Guid.NewGuid()}/cancelar",
            new ReservaCancelarDTO { Motivo = "prueba" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    // ----- 400: validación de entrada -----

    [Fact]
    public async Task RechazarReserva_SinMotivo_ComoGerente_Returns400BadRequest()
    {
        await AsGerenteAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/Reservas/{Guid.NewGuid()}/rechazar",
            new ReservaRechazarDTO { MotivoRechazo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CancelarReservaGerente_SinMotivo_ComoGerente_Returns400BadRequest()
    {
        await AsGerenteAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/Reservas/gerente/{Guid.NewGuid()}/cancelar",
            new ReservaCancelarDTO { Motivo = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }
}
