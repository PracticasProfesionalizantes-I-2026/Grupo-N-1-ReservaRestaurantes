using BusinessLogic.Usuarios.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Usuarios;
using Shared.Exceptions;

namespace Api.Controllers;

[ApiController]
[Route("api/admin/usuarios")]
[Authorize(Roles = "Admin")] // RN-01: Solo el Admin podrá gestionar roles de usuarios
public class AdminUsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public AdminUsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
    }

    /// <summary>
    /// CU-19: Gestionar roles de usuarios
    /// </summary>
    [HttpPut("{id:guid}/rol")]
    [ProducesResponseType(typeof(UsuarioResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioResponseDTO>> CambiarRol(
        [FromRoute] Guid id, 
        [FromBody] UsuarioCambiarRolDTO dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState); // 400 Bad Request
        }

        try
        {
            var resultado = await _usuarioService.CambiarRolUsuarioAsync(id, dto);
            return Ok(resultado); // 200 OK
        }
        catch (NotFoundException ex)
        {
            return Problem(
                detail: ex.Message, 
                statusCode: StatusCodes.Status404NotFound, 
                title: "Usuario No Encontrado"
            );
        }
        catch (ConflictException ex)
        {
            return Problem(
                detail: ex.Message, 
                statusCode: StatusCodes.Status409Conflict, 
                title: "Conflicto de Gobernanza"
            );
        }
        catch (ValidationException ex)
        {
            return Problem(
                detail: ex.Message, 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Rol Inválido"
            );
        }
    }
}
