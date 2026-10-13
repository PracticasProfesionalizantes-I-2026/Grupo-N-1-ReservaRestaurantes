
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
}
