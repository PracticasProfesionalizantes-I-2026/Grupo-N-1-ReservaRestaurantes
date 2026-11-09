using System.Security.Claims;
using BusinessLogic.Cliente.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.DTOs.Clientes;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Protegido por defecto: evita que usuarios anónimos accedan a información de clientes
public class ClientesController : ControllerBase
{
    private readonly IClienteService _clienteService;

    public ClientesController(IClienteService clienteService)
    {
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
    }

    /// <summary>
    /// CU-04: Registrar / Alta de un nuevo cliente
    /// Endpoint público protegido con Rate Limiting para evitar spam y abuso
    /// </summary>
    [HttpPost]
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("RegistrationPolicy")]
    [ProducesResponseType(typeof(ClienteResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ClienteResponseDTO>> Create([FromBody] ClienteCreateDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _clienteService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Obtener el perfil del cliente autenticado
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ClienteResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteResponseDTO>> GetMyProfile()
    {
        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(usuarioIdClaim, out var usuarioId))
        {
            return Unauthorized(new { message = "Token de autenticación no válido o expirado." });
        }

        var cliente = await _clienteService.GetByIdAsync(usuarioId);
        if (cliente == null) return NotFound();
        return Ok(cliente);
    }

    /// <summary>
    /// Consultar cliente por ID.
    /// Clientes solo pueden ver su propio perfil. Gerentes y Administradores pueden ver cualquier perfil.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ClienteResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteResponseDTO>> GetById(Guid id)
    {
        var rol = User.FindFirst(ClaimTypes.Role)?.Value 
               ?? User.FindFirst("role")?.Value;

        var esAdminOGerente = string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(rol, "Gerente", StringComparison.OrdinalIgnoreCase);

        if (!esAdminOGerente)
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(usuarioIdClaim, out var usuarioId) || usuarioId != id)
            {
                return Forbid();
            }
        }

        var cliente = await _clienteService.GetByIdAsync(id);
        if (cliente == null) return NotFound();
        return Ok(cliente);
    }
}
