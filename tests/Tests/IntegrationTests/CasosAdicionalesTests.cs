using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shared.DTOs.Auth;
using Shared.DTOs.Mesas;
using Shared.DTOs.Reservas;
using Xunit;

namespace Tests.IntegrationTests;

/// <summary>
/// Tests de integración de escenarios de error y flujos secundarios:
/// CU-01 (validación), CU-02 (tablero vacío), CU-05 (validación de login),
/// CU-06/CU-07/CU-09/CU-10/CU-11 (consultas) y CU-23 (eliminar mesa).
/// </summary>
public class CasosAdicionalesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static int _numeroMesa = 900;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CasosAdicionalesTests(CustomWebApplicationFactory factory)
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

    private async Task AsAsync(string email, string password)
    {
        var token = await TokenAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private Task AsClienteAsync() => AsAsync("juan.perez@example.com", "Cliente123!");
    private Task AsGerenteAsync() => AsAsync("gerente@restaurant.com", "Gerente123!");
    private Task AsAdminAsync() => AsAsync("admin@restaurant.com", "Admin123!");

    private void Limpiar() => _client.DefaultRequestHeaders.Authorization = null;

    #region CU-01: validación de solicitud de reserva

    [Fact]
    public async Task SolicitarReserva_WithMalformedJson_Returns400BadRequest()
    {
        await AsClienteAsync();
        var content = new StringContent("{ esto no es json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/Reservas/solicitar", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task SolicitarReserva_WithMissingFields_Returns400BadRequest()
    {
        await AsClienteAsync();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/Reservas/solicitar", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task SolicitarReserva_WhenOutsideHours_Returns400BadRequest()
    {
        await AsClienteAsync();
        // 03:00 cae fuera de las franjas operativas (RN-03).
        var fecha = DateTime.UtcNow.Date.AddDays(10).AddHours(3);

        var response = await _client.PostAsJsonAsync("/api/v1/Reservas/solicitar", new ReservaSolicitudDTO
        {
            FechaHora = fecha,
            CantidadComensales = 2
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-02: tablero operativo vacío

    [Fact]
    public async Task GetTableroOperativo_SinReservas_Returns200WithEmptyArray()
    {
        await AsGerenteAsync();

        var response = await _client.GetAsync("/api/v1/Reservas/lista-reservas?fecha=2000-01-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ReservaResponseDTO>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body);
        Limpiar();
    }

    #endregion

    #region CU-05: validación de login

    [Fact]
    public async Task Login_WithEmptyFields_Returns400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login",
            new LoginRequestDTO { Email = "", Password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region CU-06 / CU-07 / CU-09 / CU-10 / CU-11

    [Fact]
    public async Task GetReservasPorFecha_WhenNone_Returns200WithEmptyArray()
    {
        await AsClienteAsync();

        var response = await _client.GetAsync("/api/v1/Reservas/fecha/2000-01-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ReservaResponseDTO>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body);
        Limpiar();
    }

    [Fact]
    public async Task CancelarReserva_NonExistingId_Returns404NotFound()
    {
        await AsClienteAsync();

        var response = await _client.PatchAsync($"/api/v1/Reservas/{Guid.NewGuid()}/cancelar", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task GetContextoInicio_Returns200WithContextData()
    {
        await AsClienteAsync();

        var response = await _client.GetAsync("/api/v1/usuario/contexto-inicio");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task GetMesas_WithValidFilters_Returns200OK()
    {
        await AsGerenteAsync();

        var response = await _client.GetAsync("/api/v1/Mesas/filtrar-mapa-de-mesas?estado=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task FiltrarReservas_Returns200OK()
    {
        await AsGerenteAsync();

        var response = await _client.GetAsync("/api/v1/Reservas/filtrar-reservas-gerente");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task FiltrarReservas_AsCliente_Returns403Forbidden()
    {
        await AsClienteAsync();

        var response = await _client.GetAsync("/api/v1/Reservas/filtrar-reservas-gerente");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task FiltrarReservas_WithBadParams_Returns400BadRequest()
    {
        await AsGerenteAsync();

        var response = await _client.GetAsync("/api/v1/Reservas/filtrar-reservas-gerente?estado=999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-23: eliminar mesa (flujo principal)

    [Fact]
    public async Task EliminarMesa_Returns204NoContent()
    {
        await AsAdminAsync();
        var numero = Interlocked.Increment(ref _numeroMesa);
        var create = await _client.PostAsJsonAsync("/api/v1/Mesas", new MesaCreateDTO
        {
            Numero = numero,
            Capacidad = 2,
            Ubicacion = "Salón Principal"
        });
        create.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();

        var response = await _client.DeleteAsync($"/api/v1/Mesas/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Limpiar();
    }

    #endregion
}
