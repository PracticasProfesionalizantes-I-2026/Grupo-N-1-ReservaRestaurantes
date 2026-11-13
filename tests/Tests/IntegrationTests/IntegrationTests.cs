using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.DTOs.Auth;
using Shared.DTOs.Reservas;
using Shared.DTOs.Usuarios;
using Shared.Enums;

namespace Tests.IntegrationTests;

/// <summary>
/// Tests de integración para los flujos críticos de la API de reservas.
/// Usan WebApplicationFactory con SQLite en memoria + datos semilla.
/// 
/// Flujos cubiertos:
///   - CU-04: Registro de usuario
///   - CU-05: Login / autenticación JWT
///   - CU-01: Solicitar reserva
///   - CU-07: Cancelar reserva (cliente)
///   - CU-12: Confirmar reserva (gerente)
///   - CU-13: Iniciar reserva (check-in gerente)
///   - CU-14: Finalizar reserva (check-out gerente)
///   - CU-07/CU-08: Logout
/// </summary>
public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    // Opciones de serialización JSON: case-insensitive para deserializar propiedades de la API
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

    /// <summary>
    /// Inicia sesión con las credenciales proporcionadas y retorna el LoginResponseDTO.
    /// </summary>
    private async Task<LoginResponseDTO> LoginAsync(string email, string password)
    {
        var loginDto = new LoginRequestDTO { Email = email, Password = password };
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", loginDto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(JsonOptions);
        return result!;
    }

    /// <summary>
    /// Configura el header Authorization con el token JWT proporcionado.
    /// </summary>
    private void SetAuthToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Limpia el header Authorization.
    /// </summary>
    private void ClearAuthToken()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region CU-04: Registro de usuario

    [Fact]
    public async Task Register_ConDatosValidos_RetornaCreated()
    {
        // Arrange
        var registerDto = new UsuarioRegisterDTO
        {
            Nombre = "Nuevo",
            Apellido = "Usuario",
            Email = $"nuevo.usuario.{Guid.NewGuid():N}@test.com",
            Telefono = "+54 11 1234-5678",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/register", registerDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UsuarioResponseDTO>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(registerDto.Email, result.Email);
        Assert.Equal("Cliente", result.Rol);
    }

    [Fact]
    public async Task Register_ConEmailDuplicado_RetornaConflict()
    {
        // Arrange — juan.perez@example.com ya existe en los datos semilla
        var registerDto = new UsuarioRegisterDTO
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan.perez@example.com",
            Telefono = "+54 11 4455-6677",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/register", registerDto);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ConDatosInvalidos_RetornaBadRequest()
    {
        // Arrange — email inválido, password corta
        var registerDto = new UsuarioRegisterDTO
        {
            Nombre = "Test",
            Apellido = "User",
            Email = "no-es-un-email",
            Telefono = "+54 11 1234-5678",
            Password = "123"  // Menos de 8 caracteres
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/register", registerDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region CU-05: Login

    [Fact]
    public async Task Login_ConCredencialesValidas_RetornaTokenJWT()
    {
        // Arrange — credenciales del cliente semilla
        var loginDto = new LoginRequestDTO
        {
            Email = "juan.perez@example.com",
            Password = "Cliente123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("juan.perez@example.com", result.Email);
        Assert.Equal("Cliente", result.Role);
    }

    [Fact]
    public async Task Login_ConCredencialesInvalidas_RetornaUnauthorized()
    {
        // Arrange
        var loginDto = new LoginRequestDTO
        {
            Email = "juan.perez@example.com",
            Password = "ContraseñaIncorrecta"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", loginDto);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Gerente_RetornaRolGerente()
    {
        // Act
        var result = await LoginAsync("gerente@restaurant.com", "Gerente123!");

        // Assert
        Assert.Equal("Gerente", result.Role);
        Assert.False(string.IsNullOrEmpty(result.Token));
    }

    #endregion

    #region CU-01: Solicitar reserva

    [Fact]
    public async Task SolicitarReserva_Autenticado_RetornaCreated()
    {
        // Arrange — login como cliente
        var loginResult = await LoginAsync("carlos.rodriguez@example.com", "Cliente123!");
        SetAuthToken(loginResult.Token);

        var reservaDto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.UtcNow.Date.AddDays(7).AddHours(20), // Dentro de una semana a las 20:00
            CantidadComensales = 2,
            Observaciones = "Test de integración"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Reservas/solicitar", reservaDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ReservaEstado.Pendiente, result.Estado);
        Assert.Equal(2, result.CantidadComensales);
        Assert.Equal("Test de integración", result.Observaciones);

        ClearAuthToken();
    }

    [Fact]
    public async Task SolicitarReserva_SinAutenticacion_RetornaUnauthorized()
    {
        // Arrange — sin token
        ClearAuthToken();

        var reservaDto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.UtcNow.Date.AddDays(7).AddHours(20),
            CantidadComensales = 2
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/Reservas/solicitar", reservaDto);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region CU-07: Cancelar reserva (cliente)

    [Fact]
    public async Task CancelarReserva_ClienteDueño_RetornaOk()
    {
        // Arrange — crear una reserva para luego cancelarla
        var loginResult = await LoginAsync("laura.fernandez@example.com", "Cliente123!");
        SetAuthToken(loginResult.Token);

        var reservaDto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.UtcNow.Date.AddDays(10).AddHours(21),
            CantidadComensales = 3,
            Observaciones = "Para cancelar"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/Reservas/solicitar", reservaDto);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);

        // Act — cancelar la reserva
        var cancelDto = new ReservaCancelarDTO { Motivo = "Cambio de planes" };
        var cancelResponse = await _client.PatchAsJsonAsync($"/api/v1/Reservas/{created!.Id}/cancelar", cancelDto);

        // Assert
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var result = await cancelResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ReservaEstado.Cancelada, result.Estado);

        ClearAuthToken();
    }

    #endregion

    #region CU-12: Confirmar reserva (gerente)

    [Fact]
    public async Task ConfirmarReserva_Gerente_RetornaOk()
    {
        // Arrange — crear reserva como cliente
        var clienteLogin = await LoginAsync("maria.gonzalez@example.com", "Cliente123!");
        SetAuthToken(clienteLogin.Token);

        var reservaDto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.UtcNow.Date.AddDays(14).AddHours(13),
            CantidadComensales = 4,
            Observaciones = "Almuerzo para confirmar"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/Reservas/solicitar", reservaDto);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);

        // Cambiar a gerente para confirmar
        var gerenteLogin = await LoginAsync("gerente@restaurant.com", "Gerente123!");
        SetAuthToken(gerenteLogin.Token);

        // Act
        var confirmResponse = await _client.PatchAsync($"/api/v1/Reservas/{created!.Id}/confirmar", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var result = await confirmResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(ReservaEstado.Confirmada, result.Estado);

        ClearAuthToken();
    }

    [Fact]
    public async Task ConfirmarReserva_ComoCliente_RetornaForbidden()
    {
        // Arrange — login como cliente (no tiene rol Gerente)
        var clienteLogin = await LoginAsync("juan.perez@example.com", "Cliente123!");
        SetAuthToken(clienteLogin.Token);

        // Act — intentar confirmar cualquier reserva (el id no importa: la autorización se evalúa primero)
        var confirmResponse = await _client.PatchAsync($"/api/v1/Reservas/{Guid.NewGuid()}/confirmar", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, confirmResponse.StatusCode);

        ClearAuthToken();
    }

    #endregion

    #region CU-13 + CU-14: Flujo completo Solicitar → Confirmar → Iniciar → Finalizar

    [Fact]
    public async Task FlujoCompleto_Solicitar_Confirmar_Iniciar_Finalizar()
    {
        // 1. Solicitar reserva como cliente
        var clienteLogin = await LoginAsync("carlos.rodriguez@example.com", "Cliente123!");
        SetAuthToken(clienteLogin.Token);

        var reservaDto = new ReservaSolicitudDTO
        {
            FechaHora = DateTime.UtcNow.Date.AddDays(15).AddHours(20),
            CantidadComensales = 2,
            Observaciones = "Flujo completo integración"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/Reservas/solicitar", reservaDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.Equal(ReservaEstado.Pendiente, created!.Estado);

        // 2. Confirmar como gerente
        var gerenteLogin = await LoginAsync("gerente@restaurant.com", "Gerente123!");
        SetAuthToken(gerenteLogin.Token);

        var confirmResponse = await _client.PatchAsync($"/api/v1/Reservas/{created.Id}/confirmar", null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.Equal(ReservaEstado.Confirmada, confirmed!.Estado);

        // 3. Iniciar (check-in) como gerente
        var iniciarResponse = await _client.PatchAsync($"/api/v1/Reservas/{created.Id}/iniciar", null);
        Assert.Equal(HttpStatusCode.OK, iniciarResponse.StatusCode);
        var iniciada = await iniciarResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.Equal(ReservaEstado.EnCurso, iniciada!.Estado);

        // 4. Finalizar (check-out) como gerente
        var finalizarResponse = await _client.PatchAsync($"/api/v1/Reservas/{created.Id}/finalizar", null);
        Assert.Equal(HttpStatusCode.OK, finalizarResponse.StatusCode);
        var finalizada = await finalizarResponse.Content.ReadFromJsonAsync<ReservaResponseDTO>(JsonOptions);
        Assert.Equal(ReservaEstado.Finalizada, finalizada!.Estado);

        ClearAuthToken();
    }

    #endregion

    #region CU-07/CU-08: Logout

    [Fact]
    public async Task Logout_ConTokenValido_RetornaOk()
    {
        // Arrange: usuario propio para no revocar tokens de usuarios compartidos con otros tests
        // (la revocación por usuario es global dentro del host de tests).
        var email = $"logout.{Guid.NewGuid():N}@test.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/Auth/register", new UsuarioRegisterDTO
        {
            Nombre = "Logout",
            Apellido = "Test",
            Email = email,
            Telefono = "+54 11 1234-5678",
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResult = await LoginAsync(email, "Password123!");
        SetAuthToken(loginResult.Token);

        // Act
        var response = await _client.PostAsync("/api/v1/Auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        ClearAuthToken();
    }

    [Fact]
    public async Task Logout_SinToken_RetornaUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await _client.PostAsync("/api/v1/Auth/logout", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Acceso no autorizado a endpoints protegidos

    [Fact]
    public async Task ListaEspera_SinAutenticacion_RetornaUnauthorized()
    {
        ClearAuthToken();
        var response = await _client.GetAsync("/api/v1/Reservas/lista-espera");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListaReservas_ComoCliente_RetornaForbidden()
    {
        var login = await LoginAsync("juan.perez@example.com", "Cliente123!");
        SetAuthToken(login.Token);

        var response = await _client.GetAsync("/api/v1/Reservas/lista-reservas");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        ClearAuthToken();
    }

    #endregion

    #region CU-06: Filtrar reservas por fecha

    [Fact]
    public async Task FiltrarPorFecha_ClienteAutenticado_RetornaOk()
    {
        // Arrange
        var login = await LoginAsync("juan.perez@example.com", "Cliente123!");
        SetAuthToken(login.Token);

        var fecha = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/v1/Reservas/fecha/{fecha}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ReservaResponseDTO>>(JsonOptions);
        Assert.NotNull(result);

        ClearAuthToken();
    }

    #endregion
}

