using System.Security.Claims;
using BusinessLogic.Usuarios.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Usuarios;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/usuario")]
[Authorize]
public class UsuarioController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuarioController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
    }

    /// <summary>
    /// CU-09: Volver a pantalla inicial / Contexto de inicio.
    /// Retorna los datos del usuario, accesos directos y métricas de resumen del día adaptadas a su rol (Cliente, Gerente o Admin).
    /// </summary>
    [HttpGet("contexto-inicio")]
    [ProducesResponseType(typeof(ContextoInicioDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContextoInicioDTO>> GetContextoInicio()
    {
        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(usuarioIdClaim, out var usuarioId))
        {
            return Unauthorized(new { message = "Token de autenticación no válido o expirado." });
        }

        var rol = User.FindFirst(ClaimTypes.Role)?.Value
               ?? User.FindFirst("role")?.Value;

        var resultado = await _usuarioService.GetContextoInicioAsync(usuarioId, rol);
        return Ok(resultado);
    }
}

