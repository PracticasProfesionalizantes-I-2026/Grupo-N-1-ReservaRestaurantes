
using BusinessLogic.Usuarios.Interfaces;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Usuarios;
using Shared.Exceptions;

namespace BusinessLogic.Usuarios.Implementations;

public class UsuarioService : IUsuarioService
{
    private static readonly HashSet<string> RolesValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Gerente",
        "Cliente"
    };

    private readonly IClienteRepository _clienteRepository;

    public UsuarioService(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
    }

    public async Task<UsuarioResponseDTO> CambiarRolUsuarioAsync(Guid id, UsuarioCambiarRolDTO dto)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        // RN-02: Validar rol existente y habilitado en el catálogo
        var nuevoRolNormalizado = dto.NuevoRol.Trim();
        if (!RolesValidos.Contains(nuevoRolNormalizado))
        {
            throw new ValidationException("El rol especificado no es válido en el sistema.");
        }

        // 3a: Verificar existencia del usuario
        var usuario = await _clienteRepository.GetByIdAsync(id);
        if (usuario == null)
        {
            throw new NotFoundException($"Usuario con ID {id} no fue encontrado.");
        }

        // 3b: Salvaguarda - Prohibir quitar el rol Admin si es el único administrador activo
        bool eraAdmin = string.Equals(usuario.Rol, "Admin", StringComparison.OrdinalIgnoreCase);
        bool dejaDeSerAdmin = !string.Equals(nuevoRolNormalizado, "Admin", StringComparison.OrdinalIgnoreCase);

        if (eraAdmin && dejaDeSerAdmin)
        {
            int adminsActivos = await _clienteRepository.CountActiveAdminsAsync();
            if (adminsActivos <= 1)
            {
                throw new ConflictException("No se puede quitar el rol de Administrador al único administrador activo del sistema.");
            }
        }

        // Actualización de rol
        usuario.Rol = nuevoRolNormalizado;
        await _clienteRepository.UpdateAsync(usuario);

        return new UsuarioResponseDTO
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Rol = usuario.Rol,
            Activo = usuario.Activo,
            FechaRegistro = usuario.FechaRegistro
        };
    }

    public async Task<bool> EliminarUsuarioAsync(Guid id, Guid adminEnSesionId)
    {
        // 3b: Salvaguarda - Prohibir auto-eliminación de la cuenta en sesión (RN / 409 Conflict)
        if (id == adminEnSesionId)
        {
            throw new ConflictException("No es posible eliminar la propia cuenta de administrador en sesión.");
        }

        // 3a: Verificar existencia del usuario (404 Not Found)
        var usuario = await _clienteRepository.GetByIdAsync(id);
        if (usuario == null)
        {
            throw new NotFoundException($"Usuario con ID {id} no fue encontrado.");
        }

        // Salvaguarda: Prohibir eliminar al único administrador activo
        if (string.Equals(usuario.Rol, "Admin", StringComparison.OrdinalIgnoreCase) && usuario.Activo)
        {
            int adminsActivos = await _clienteRepository.CountActiveAdminsAsync();
            if (adminsActivos <= 1)
            {
                throw new ConflictException("No se puede eliminar al único administrador activo del sistema.");
            }
        }

        // RN-03: Evaluar si posee reservas vinculadas en su histórico
        bool tieneReservas = await _clienteRepository.HasReservationsAsync(id);

        if (tieneReservas)
        {
            // Baja lógica: preservar datos históricos e integridad referencial
            usuario.Activo = false;
            await _clienteRepository.UpdateAsync(usuario);
            return false; // Indica que se aplicó baja lógica
        }
        else
        {
            // Baja física: el usuario no tiene historial de reservas
            await _clienteRepository.DeleteAsync(id);
            return true; // Indica que se aplicó baja física
        }
    }
}
