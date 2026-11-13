using System.Security.Claims;
using BusinessLogic.Reservas.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Reservas;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReservasController : ControllerBase
{
    private readonly IReservaService _reservaService;

    public ReservasController(IReservaService reservaService)
    {
        _reservaService = reservaService ?? throw new ArgumentNullException(nameof(reservaService));
    }

    /// <summary>
    /// CU-01: Solicitar Reserva
    /// </summary>
    [HttpPost("solicitar")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> Solicitar([FromBody] ReservaSolicitudDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Obtener ClienteId desde el Token JWT (soporta ClaimTypes.NameIdentifier y 'sub')
        var clienteIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(clienteIdClaim, out var clienteId))
        {
            return Unauthorized(new { message = "Token de autenticación no válido o expirado." });
        }

        var result = await _reservaService.SolicitarReservaAsync(clienteId, dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservaResponseDTO>> GetById(Guid id)
    {
        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        Guid? usuarioId = Guid.TryParse(usuarioIdClaim, out var parsedId) ? parsedId : null;
        var rol = User.FindFirst(ClaimTypes.Role)?.Value 
               ?? User.FindFirst("role")?.Value;

        var result = await _reservaService.GetByIdAsync(id, usuarioId, rol);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// RF-07 / CU-08: Modificar Reserva
    /// Permite al cliente (dueño) o a un Gerente/Admin modificar fecha, hora, comensales, mesa y observaciones de una reserva.
    /// </summary>
    [HttpPut("{id:guid}")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> Modificar(Guid id, [FromBody] ReservaUpdateDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        Guid? usuarioId = Guid.TryParse(usuarioIdClaim, out var parsedId) ? parsedId : null;
        var rol = User.FindFirst(ClaimTypes.Role)?.Value 
               ?? User.FindFirst("role")?.Value;

        var result = await _reservaService.ModificarReservaAsync(id, dto, usuarioId, rol);
        return Ok(result);
    }

    /// <summary>
    /// CU-07: Cancelar Reserva (Cliente)
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> Cancelar(Guid id, [FromBody] ReservaCancelarDTO? dto)
    {
        // Obtener ClienteId desde el Token JWT
        var clienteIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clienteIdClaim, out var clienteId))
        {
            return Unauthorized(new { message = "Token de autenticación no válido o expirado." });
        }

        var result = await _reservaService.CancelarReservaClienteAsync(id, clienteId, dto);
        return Ok(result);
    }

    /// <summary>
    /// CU-06: Filtrar reservas por fecha
    /// </summary>
    [HttpGet("fecha/{fecha:datetime}")]
    [ProducesResponseType(typeof(IEnumerable<ReservaResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<ReservaResponseDTO>>> FiltrarPorFecha(DateTime fecha)
    {
        // Obtener ClienteId desde el Token JWT
        var clienteIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clienteIdClaim, out var clienteId))
        {
            return Unauthorized(new { message = "Token de autenticación no válido o expirado." });
        }

        var reservas = await _reservaService.FiltrarPorFechaAsync(clienteId, fecha);
        return Ok(reservas);
    }

    /// <summary>
    /// Confirmar Reserva (Solo Gerentes) - CU-12
    /// </summary>
    [HttpPatch("{id:guid}/confirmar")]
    [HttpPut("{id:guid}/confirmar")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> ConfirmarReserva(Guid id)
    {
        var response = await _reservaService.ConfirmarReservaAsync(id);
        return Ok(response);
    }

    /// <summary>
    /// CU-13: Iniciar Reserva (Check-In)
    /// </summary>
    [HttpPatch("{id:guid}/iniciar")]
    [Authorize(Roles = "Gerente")] // RN-02
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> IniciarReserva(Guid id)
    {
        var result = await _reservaService.IniciarReservaAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// CU-14: Finalizar Reserva (Check-Out y liberar mesa)
    /// </summary>
    [HttpPatch("{id:guid}/finalizar")]
    [Authorize(Roles = "Gerente")] // RN-03
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> FinalizarReserva(Guid id)
    {
        var result = await _reservaService.FinalizarReservaAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// RF-10 / CU-10: Cambio genérico / manual de estado de reserva (Solo Gerente y Admin)
    /// Aplica validación estricta de máquina de estados del restaurante y actualiza la mesa asociada.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [HttpPut("{id:guid}/estado")]
    [Authorize(Roles = "Gerente,Admin")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> CambiarEstado(Guid id, [FromBody] ReservaCambiarEstadoDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _reservaService.CambiarEstadoAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// CU-15: Cancelar Reserva (Gerente - Cancelación Manual)
    /// </summary>
    [HttpPatch("gerente/{id:guid}/cancelar")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> CancelarReservaGerente(Guid id, [FromBody] ReservaCancelarDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _reservaService.CancelarReservaGerenteAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// CU-16: Rechazar Reserva (Gerente) - Solo aplica a reservas Pendientes
    /// </summary>
    [HttpPatch("{id:guid}/rechazar")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> RechazarReserva(Guid id, [FromBody] ReservaRechazarDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _reservaService.RechazarReservaAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// CU-17: Elegir / Reasignar mesa de reserva (Solo Gerente)
    /// </summary>
    [HttpPatch("{id:guid}/asignar-mesa")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> AsignarMesa(Guid id, [FromBody] ReservaAsignarMesaDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _reservaService.AsignarMesaAsync(id, dto.MesaId);
        return Ok(result);
    }

    /// <summary>
    /// CU-18: Ver listado de reservas en espera (Reservas Pendientes - Solo Gerente)
    /// </summary>
    [HttpGet("lista-espera")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(IEnumerable<ReservaResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ReservaResponseDTO>>> GetListaEspera()
    {
        var result = await _reservaService.GetListaEsperaAsync();
        return Ok(result);
    }

    /// <summary>
    /// RF-13 / CU-18: Promover reserva en lista de espera a confirmada asignando mesa liberada/disponible
    /// </summary>
    [HttpPatch("lista-espera/{id:guid}/promover")]
    [HttpPatch("{id:guid}/promover")]
    [Authorize(Roles = "Gerente,Admin")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> PromoverListaEspera(Guid id, [FromBody] ReservaPromoverDTO? dto = null)
    {
        var result = await _reservaService.PromoverListaEsperaAsync(id, dto?.MesaId);
        return Ok(result);
    }

    /// <summary>
    /// CU-02: Tablero Operativo de Reservas (Solo Gerente)
    /// </summary>
    [HttpGet("lista-reservas")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(IEnumerable<ReservaResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ReservaResponseDTO>>> GetTableroOperativo([FromQuery] DateTime? fecha = null)
    {
        var result = await _reservaService.GetTableroOperativoAsync(fecha);
        return Ok(result);
    }

    /// <summary>
    /// CU-11: Filtrar reservas (Solo Gerente).
    /// Permite combinar los filtros: fecha, estado, mesaId y clienteId. Todos opcionales.
    /// </summary>
    [HttpGet("filtrar-reservas")]
    [HttpGet("filtrar-reservas-gerente")]
    [Authorize(Roles = "Gerente")]
    [ProducesResponseType(typeof(IEnumerable<ReservaResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ReservaResponseDTO>>> FiltrarReservas([FromQuery] ReservaFiltroDTO filtro)
    {
        var result = await _reservaService.FiltrarReservasAsync(filtro);
        return Ok(result);
    }
}
