using BusinessLogic.Reservas.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Reservas;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/lista-espera")]
[Authorize(Roles = "Gerente,Admin")]
public class ListaEsperaController : ControllerBase
{
    private readonly IListaEsperaService _listaEsperaService;

    public ListaEsperaController(IListaEsperaService listaEsperaService)
    {
        _listaEsperaService = listaEsperaService ?? throw new ArgumentNullException(nameof(listaEsperaService));
    }

    /// <summary>
    /// CU-18 / RN-01, RN-02: Consultar solicitudes en lista de espera activas (orden FIFO).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReservaResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ReservaResponseDTO>>> GetSolicitudes()
    {
        var result = await _listaEsperaService.GetSolicitudesOrdenadasAsync();
        return Ok(result);
    }

    /// <summary>
    /// CU-18 / RF-13 / RN-03: Promover una solicitud de lista de espera a reserva Confirmada,
    /// asignándole una mesa compatible disponible y notificando al cliente.
    /// </summary>
    [HttpPatch("{id:guid}/promover")]
    [ProducesResponseType(typeof(ReservaResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservaResponseDTO>> Promover(Guid id, [FromBody] ReservaPromoverDTO? dto = null)
    {
        var result = await _listaEsperaService.PromoverSolicitudAsync(id, dto?.MesaId);
        return Ok(result);
    }
}

