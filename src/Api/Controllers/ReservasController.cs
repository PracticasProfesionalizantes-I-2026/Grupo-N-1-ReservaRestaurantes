using System.Security.Claims;
using BusinessLogic.Reservas.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Reservas;
using Shared.Exceptions;

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

        try
        {
            var result = await _reservaService.SolicitarReservaAsync(clienteId, dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (HorarioNoHabilitadoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Horario No Habilitado");
        }
        catch (ConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Ya existe una reserva para ese horario.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReservaResponseDTO>> GetById(Guid id)
    {
        var result = await _reservaService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// CU-07: Cancelar Reserva (Cliente)
    /// </summary>
    [HttpPatch("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        try
        {
            var result = await _reservaService.CancelarReservaClienteAsync(id, clienteId, dto);
            return Ok(result);
        }
        catch (ReservaNoEncontradaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Reserva No Encontrada");
        }
        catch (TransicionEstadoInvalidaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Estado No Cancelable");
        }
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
    /// Confirmar Reserva (Solo Gerentes)
    /// </summary>
    [HttpPut("{id:guid}/confirmar")]
    [Authorize(Roles = "Gerente")] 
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReservaResponseDTO>> ConfirmarReserva(Guid id)
    {
        try
        {
            var response = await _reservaService.ConfirmarReservaAsync(id);
            return Ok(response);
        }
        catch (ReservaNoEncontradaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Reserva No Encontrada");
        }
        catch (TransicionEstadoInvalidaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Estado Inválido para Confirmar");
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Error Interno del Servidor");
        }
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReservaResponseDTO>> IniciarReserva(Guid id)
    {
        try
        {
            var result = await _reservaService.IniciarReservaAsync(id);
            return Ok(result);
        }
        catch (ReservaNoEncontradaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Reserva No Encontrada");
        }
        catch (TransicionEstadoInvalidaException ex)
        {
            // Retorna 409 Conflict si la reserva no estaba Confirmada
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Estado Inválido para Iniciar");
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Error Interno del Servidor");
        }
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReservaResponseDTO>> FinalizarReserva(Guid id)
    {
        try
        {
            var result = await _reservaService.FinalizarReservaAsync(id);
            return Ok(result);
        }
        catch (ReservaNoEncontradaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Reserva No Encontrada");
        }
        catch (TransicionEstadoInvalidaException ex)
        {
            // Retorna 409 Conflict si la reserva no estaba EnCurso
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Estado Inválido para Finalizar");
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Error Interno del Servidor");
        }
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReservaResponseDTO>> CancelarReservaGerente(Guid id)
    {
        try
        {
            var result = await _reservaService.CancelarReservaGerenteAsync(id);
            return Ok(result);
        }
        catch (ReservaNoEncontradaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Reserva No Encontrada");
        }
        catch (TransicionEstadoInvalidaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Estado Inválido para Cancelar");
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Error Interno del Servidor");
        }
    }


}
