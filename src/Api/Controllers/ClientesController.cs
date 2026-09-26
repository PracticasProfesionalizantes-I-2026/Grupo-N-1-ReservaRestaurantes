using BusinessLogic.Cliente.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Clientes;
using Shared.Exceptions;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ClientesController : ControllerBase
{
    private readonly IClienteService _clienteService;

    public ClientesController(IClienteService clienteService)
    {
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
    }

    /// <summary>
    /// CU-04: Registrar / Alta de un nuevo cliente
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClienteResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteResponseDTO>> Create([FromBody] ClienteCreateDTO dto)
    {
        // 1. Validación sintáctica de la Capa de Presentación
        // Esto es redundante pero lo dejamos para entender api[controller]
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState); // Devuelve HTTP 400
        }

        try
        {
            // 2. Invocación de la Capa de Negocio
            var result = await _clienteService.CreateAsync(dto);

            // 3. Respuesta HTTP 201 Created con Location Header
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ConflictException ex)
        {
            // Captura de excepción de regla de negocio RN-01 -> HTTP 409
            return Problem(detail: ex.Message, statusCode: 409, title: "Conflicto de Regla de Negocio");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClienteResponseDTO>> GetById(Guid id)
    {
        var cliente = await _clienteService.GetByIdAsync(id);
        if (cliente == null) return NotFound();
        return Ok(cliente);
    }
}
