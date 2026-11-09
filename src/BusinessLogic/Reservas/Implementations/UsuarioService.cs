
using BusinessLogic.Usuarios.Interfaces;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Reservas;
using Shared.DTOs.Usuarios;
using Shared.Enums;
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
    private readonly IReservaRepository? _reservaRepository;
    private readonly IMesaRepository? _mesaRepository;
    private readonly BusinessLogic.Auth.Interfaces.ITokenRevocationService? _tokenRevocationService;

    public UsuarioService(
        IClienteRepository clienteRepository,
        IReservaRepository? reservaRepository = null,
        IMesaRepository? mesaRepository = null,
        BusinessLogic.Auth.Interfaces.ITokenRevocationService? tokenRevocationService = null)
    {
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _reservaRepository = reservaRepository;
        _mesaRepository = mesaRepository;
        _tokenRevocationService = tokenRevocationService;
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

        // RN-03 / SEC-07: Revocación de sesiones y tokens activos al cambiar de rol
        _tokenRevocationService?.RevokeUserTokens(id);

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

            // SEC-07: Revocar tokens activos al desactivar cuenta
            _tokenRevocationService?.RevokeUserTokens(id);

            return false; // Indica que se aplicó baja lógica
        }
        else
        {
            // Baja física: el usuario no tiene historial de reservas
            await _clienteRepository.DeleteAsync(id);

            // SEC-07: Revocar tokens activos al eliminar cuenta
            _tokenRevocationService?.RevokeUserTokens(id);

            return true; // Indica que se aplicó baja física
        }
    }

    /// <inheritdoc />
    public async Task<ContextoInicioDTO> GetContextoInicioAsync(Guid usuarioId, string? rol = null)
    {
        // 1. Validar existencia del usuario
        var usuario = await _clienteRepository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new NotFoundException($"Usuario con ID {usuarioId} no fue encontrado.");
        }

        if (!usuario.Activo)
        {
            throw new AccesoDenegadoException("La cuenta de usuario se encuentra inactiva.");
        }

        // 2. Determinar rol efectivo (RN-01 de CU-09)
        var rolEfectivo = !string.IsNullOrWhiteSpace(rol) ? rol.Trim() : usuario.Rol?.Trim() ?? "Cliente";
        if (!RolesValidos.Contains(rolEfectivo))
        {
            throw new AccesoDenegadoException($"El rol '{rolEfectivo}' no está habilitado para acceder al contexto inicial.");
        }

        var contexto = new ContextoInicioDTO
        {
            UsuarioId = usuario.Id,
            NombreCompleto = $"{usuario.Nombre} {usuario.Apellido}".Trim(),
            Email = usuario.Email,
            Rol = rolEfectivo,
            MensajeBienvenida = $"¡Bienvenido/a, {usuario.Nombre}!"
        };

        // 3. Resolver datos y accesos rápidos según el rol
        if (string.Equals(rolEfectivo, "Cliente", StringComparison.OrdinalIgnoreCase))
        {
            contexto.AccesosRapidos = new List<string> { "Mis Reservas", "Solicitar Reserva", "Mi Perfil" };

            var totalActivas = 0;
            var proximas = new List<ReservaResponseDTO>();

            if (_reservaRepository != null)
            {
                var reservas = (await _reservaRepository.GetAllAsync(clienteId: usuario.Id)).ToList();
                var activas = reservas
                    .Where(r => r.Estado == ReservaEstado.Pendiente || 
                                r.Estado == ReservaEstado.Confirmada || 
                                r.Estado == ReservaEstado.EnCurso)
                    .ToList();

                totalActivas = activas.Count;
                proximas = activas
                    .OrderBy(r => r.FechaHora)
                    .Take(5)
                    .Select(MapToReservaResponseDTO)
                    .ToList();
            }

            contexto.ResumenCliente = new ResumenClienteDTO
            {
                TotalReservasActivas = totalActivas,
                ProximasReservas = proximas
            };
        }
        else if (string.Equals(rolEfectivo, "Gerente", StringComparison.OrdinalIgnoreCase))
        {
            contexto.AccesosRapidos = new List<string> { "Tablero Operativo", "Lista de Espera", "Gestión de Mesas", "Filtrar Reservas" };

            int totalMesas = 0, libres = 0, ocupadas = 0, reservasHoy = 0, enEspera = 0;

            if (_mesaRepository != null)
            {
                var mesas = (await _mesaRepository.GetAllAsync()).ToList();
                totalMesas = mesas.Count;
                libres = mesas.Count(m => m.Estado == MesaEstado.Libre && m.Activa);
                ocupadas = mesas.Count(m => m.Estado == MesaEstado.Ocupada);
            }

            if (_reservaRepository != null)
            {
                var reservasDelDia = (await _reservaRepository.GetAllAsync(fecha: DateTime.Today)).ToList();
                reservasHoy = reservasDelDia.Count;

                var listaEspera = (await _reservaRepository.GetAllAsync(estado: ReservaEstado.Pendiente)).ToList();
                enEspera = listaEspera.Count;
            }

            contexto.ResumenGerente = new ResumenGerenteDTO
            {
                TotalMesas = totalMesas,
                MesasLibres = libres,
                MesasOcupadas = ocupadas,
                ReservasHoy = reservasHoy,
                ReservasPendientesListaEspera = enEspera
            };
        }
        else if (string.Equals(rolEfectivo, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            contexto.AccesosRapidos = new List<string> { "Gestión de Usuarios", "Tablero Operativo", "Configuración del Sistema" };

            var usuarios = (await _clienteRepository.GetAllAsync()).ToList();
            var admins = await _clienteRepository.CountActiveAdminsAsync();
            var totalMesas = _mesaRepository != null ? (await _mesaRepository.GetAllAsync()).Count() : 0;
            var reservasHoy = _reservaRepository != null ? (await _reservaRepository.GetAllAsync(fecha: DateTime.Today)).Count() : 0;

            contexto.ResumenAdmin = new ResumenAdminDTO
            {
                TotalUsuarios = usuarios.Count,
                AdminsActivos = admins,
                TotalMesas = totalMesas,
                TotalReservasHoy = reservasHoy
            };
        }

        return contexto;
    }

    private static ReservaResponseDTO MapToReservaResponseDTO(Reserva r) => new()
    {
        Id = r.Id,
        ClienteId = r.ClienteId,
        ClienteNombreCompleto = r.Cliente != null ? $"{r.Cliente.Nombre} {r.Cliente.Apellido}".Trim() : string.Empty,
        ClienteEmail = r.Cliente?.Email ?? string.Empty,
        ClienteTelefono = r.Cliente?.Telefono ?? string.Empty,
        MesaId = r.MesaId,
        MesaNumero = r.Mesa?.Numero ?? 0,
        MesaCapacidad = r.Mesa?.Capacidad ?? 0,
        MesaUbicacion = r.Mesa?.Ubicacion ?? string.Empty,
        FechaHora = r.FechaHora,
        CantidadComensales = r.CantidadComensales,
        DuracionEstimadaMinutos = r.DuracionEstimadaMinutos,
        Estado = r.Estado,
        Observaciones = r.Observaciones,
        FechaCreacion = r.FechaCreacion
    };
}
