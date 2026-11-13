using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.DTOs.Auth;
using Shared.DTOs.Mesas;
using Shared.DTOs.Usuarios;
using Xunit;

namespace Tests.IntegrationTests;

/// <summary>
/// Tests de integración para gestión de mesas (CU-03, CU-10, CU-21 a CU-23)
/// y de usuarios (CU-19, CU-20). Cubren los escenarios HTTP de autorización,
/// validación y recursos inexistentes de las matrices de trazabilidad.
/// </summary>
public class MesasYUsuariosTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static int _numeroMesa = 700;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MesasYUsuariosTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Helpers

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

    private Task AsAdminAsync() => AsAsync("admin@restaurant.com", "Admin123!");
    private Task AsGerenteAsync() => AsAsync("gerente@restaurant.com", "Gerente123!");
    private Task AsClienteAsync() => AsAsync("juan.perez@example.com", "Cliente123!");

    private void Limpiar() => _client.DefaultRequestHeaders.Authorization = null;

    private static int NuevoNumeroMesa() => Interlocked.Increment(ref _numeroMesa);

    /// <summary>Crea una mesa como Admin y devuelve su Id.</summary>
    private async Task<Guid> CrearMesaAsync(int numero)
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/v1/Mesas", new MesaCreateDTO
        {
            Numero = numero,
            Capacidad = 4,
            Ubicacion = "Salón Principal"
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Registra un usuario nuevo y devuelve su Id y email.</summary>
    private async Task<(Guid Id, string Email)> RegistrarUsuarioAsync()
    {
        var email = $"tmp.{Guid.NewGuid():N}@test.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/Auth/register", new UsuarioRegisterDTO
        {
            Nombre = "Temporal",
            Apellido = "Usuario",
            Email = email,
            Telefono = "+54 11 1234-5678",
            Password = "Password123!"
        });
        reg.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("id").GetGuid(), email);
    }

    #endregion

    #region CU-21: Crear mesa

    [Fact]
    public async Task CrearMesa_Returns201Created()
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/v1/Mesas", new MesaCreateDTO
        {
            Numero = NuevoNumeroMesa(),
            Capacidad = 4,
            Ubicacion = "Salón Principal"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CrearMesa_AsGerente_Returns403Forbidden()
    {
        await AsGerenteAsync();
        var response = await _client.PostAsJsonAsync("/api/v1/Mesas", new MesaCreateDTO
        {
            Numero = NuevoNumeroMesa(),
            Capacidad = 4
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CrearMesa_WithZeroCapacity_Returns400BadRequest()
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/v1/Mesas", new MesaCreateDTO
        {
            Numero = NuevoNumeroMesa(),
            Capacidad = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CrearMesa_DuplicateNumero_Returns409Conflict()
    {
        var numero = NuevoNumeroMesa();
        await CrearMesaAsync(numero);
        await AsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/Mesas", new MesaCreateDTO
        {
            Numero = numero,
            Capacidad = 4
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-22: Modificar mesa

    [Fact]
    public async Task ModificarMesa_Returns204NoContent()
    {
        var id = await CrearMesaAsync(NuevoNumeroMesa());
        await AsAdminAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/Mesas/{id}", new MesaUpdateDTO
        {
            Numero = NuevoNumeroMesa(),
            Capacidad = 6,
            Ubicacion = "Terraza"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task ModificarMesa_WithInvalidCapacity_Returns400BadRequest()
    {
        var id = await CrearMesaAsync(NuevoNumeroMesa());
        await AsAdminAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/Mesas/{id}", new MesaUpdateDTO
        {
            Numero = NuevoNumeroMesa(),
            Capacidad = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task ModificarMesa_NonExistingId_Returns404NotFound()
    {
        await AsAdminAsync();
        var response = await _client.PutAsJsonAsync($"/api/v1/Mesas/{Guid.NewGuid()}", new MesaUpdateDTO
        {
            Numero = NuevoNumeroMesa(),
            Capacidad = 4
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-23: Eliminar mesa

    [Fact]
    public async Task EliminarMesa_AsGerente_Returns403Forbidden()
    {
        var id = await CrearMesaAsync(NuevoNumeroMesa());
        await AsGerenteAsync();

        var response = await _client.DeleteAsync($"/api/v1/Mesas/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task EliminarMesa_NonExistingId_Returns404NotFound()
    {
        await AsAdminAsync();
        var response = await _client.DeleteAsync($"/api/v1/Mesas/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-03: Cambiar estado de mesa

    [Fact]
    public async Task CambiarEstadoMesa_WithInvalidState_Returns400BadRequest()
    {
        var id = await CrearMesaAsync(NuevoNumeroMesa());
        await AsGerenteAsync();

        var response = await _client.PatchAsJsonAsync($"/api/v1/Mesas/{id}/estado",
            new { nuevoEstado = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CambiarEstadoMesa_NonExistingId_Returns404NotFound()
    {
        await AsGerenteAsync();
        var response = await _client.PatchAsJsonAsync($"/api/v1/Mesas/{Guid.NewGuid()}/estado",
            new { nuevoEstado = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-10: Filtrar mapa de mesas

    [Fact]
    public async Task GetMesas_AsCliente_Returns403Forbidden()
    {
        await AsClienteAsync();
        var response = await _client.GetAsync("/api/v1/Mesas/filtrar-mapa-de-mesas");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task GetMesas_WhenNoMatch_Returns200WithEmptyArray()
    {
        await AsGerenteAsync();
        var response = await _client.GetAsync("/api/v1/Mesas/filtrar-mapa-de-mesas?capacidadMinima=999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<MesaResponseDTO>>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body);
        Limpiar();
    }

    [Fact]
    public async Task GetMesas_WithInvalidState_Returns400BadRequest()
    {
        await AsGerenteAsync();
        var response = await _client.GetAsync("/api/v1/Mesas/filtrar-mapa-de-mesas?estado=999");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-19: Gestionar roles de usuarios

    [Fact]
    public async Task CambiarRolUsuario_Returns200OK()
    {
        var (id, _) = await RegistrarUsuarioAsync();
        await AsAdminAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/usuarios/{id}/rol",
            new UsuarioCambiarRolDTO { NuevoRol = "Gerente" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CambiarRolUsuario_AsGerente_Returns403Forbidden()
    {
        var (id, _) = await RegistrarUsuarioAsync();
        await AsGerenteAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/usuarios/{id}/rol",
            new UsuarioCambiarRolDTO { NuevoRol = "Gerente" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CambiarRolUsuario_WithInvalidRole_Returns400BadRequest()
    {
        var (id, _) = await RegistrarUsuarioAsync();
        await AsAdminAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/usuarios/{id}/rol",
            new UsuarioCambiarRolDTO { NuevoRol = "RolInexistente" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task CambiarRolUsuario_NonExistingId_Returns404NotFound()
    {
        await AsAdminAsync();
        var response = await _client.PutAsJsonAsync($"/api/v1/admin/usuarios/{Guid.NewGuid()}/rol",
            new UsuarioCambiarRolDTO { NuevoRol = "Gerente" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    #endregion

    #region CU-20: Eliminar usuarios

    [Fact]
    public async Task EliminarUsuario_Returns200OK()
    {
        var (id, _) = await RegistrarUsuarioAsync();
        await AsAdminAsync();

        var response = await _client.DeleteAsync($"/api/v1/admin/usuarios/{id}?confirmar=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task EliminarUsuario_AsGerente_Returns403Forbidden()
    {
        var (id, _) = await RegistrarUsuarioAsync();
        await AsGerenteAsync();

        var response = await _client.DeleteAsync($"/api/v1/admin/usuarios/{id}?confirmar=true");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task EliminarUsuario_WithoutConfirm_Returns400BadRequest()
    {
        var (id, _) = await RegistrarUsuarioAsync();
        await AsAdminAsync();

        var response = await _client.DeleteAsync($"/api/v1/admin/usuarios/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task EliminarUsuario_NonExistingId_Returns404NotFound()
    {
        await AsAdminAsync();
        var response = await _client.DeleteAsync($"/api/v1/admin/usuarios/{Guid.NewGuid()}?confirmar=true");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Limpiar();
    }

    [Fact]
    public async Task EliminarUsuario_SelfDelete_Returns409Conflict()
    {
        await AsAdminAsync();
        var me = await _client.GetAsync("/api/v1/Clientes/me");
        me.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var miId = doc.RootElement.GetProperty("id").GetGuid();

        var response = await _client.DeleteAsync($"/api/v1/admin/usuarios/{miId}?confirmar=true");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Limpiar();
    }

    #endregion
}
