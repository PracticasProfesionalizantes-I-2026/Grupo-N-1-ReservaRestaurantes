using BusinessLogic.Mesas.Interfaces;
using Shared.Enums;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Mesas;
using Shared.Exceptions;

namespace BusinessLogic.Mesas.Implementations;

public class MesaService : IMesaService
{
    private readonly IMesaRepository _mesaRepository;
    private readonly IReservaRepository? _reservaRepository;

    public MesaService(IMesaRepository mesaRepository, IReservaRepository? reservaRepository = null)
    {
        _mesaRepository = mesaRepository ?? throw new ArgumentNullException(nameof(mesaRepository));
        _reservaRepository = reservaRepository;
    }

    public async Task<MesaResponseDTO?> GetByIdAsync(Guid id)
    {
        var mesa = await _mesaRepository.GetByIdAsync(id);
        if (mesa == null) return null;

        return new MesaResponseDTO
        {
            Id = mesa.Id,
            Numero = mesa.Numero,
            Capacidad = mesa.Capacidad,
            Ubicacion = mesa.Ubicacion,
            Estado = mesa.Estado,
            Activa = mesa.Activa
        };
    }

    public async Task<MesaResponseDTO> CreateAsync(MesaCreateDTO dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        if (dto.Capacidad <= 0)
        {
            throw new ValidationException(nameof(dto.Capacidad), "La capacidad de la mesa debe ser mayor a cero.");
        }

        // 1. Validar regla de negocio: No pueden existir dos mesas con el mismo número
        var existeMesa = await _mesaRepository.ExistsByNumeroAsync(dto.Numero);
        if (existeMesa)
        {
            throw new ConflictException($"Ya existe una mesa registrada con el número {dto.Numero}.");
        }

        // 2. Mapear de DTO a Entidad
        var nuevaMesa = new Mesa
        {
            Numero = dto.Numero,
            Capacidad = dto.Capacidad,
            Ubicacion = string.IsNullOrWhiteSpace(dto.Ubicacion) ? "Salón Principal" : dto.Ubicacion.Trim(),
            Estado = MesaEstado.Libre,
            Activa = true
        };

        // 3. Persistir en la base de datos
        var mesaCreada = await _mesaRepository.CreateAsync(nuevaMesa);

        // 4. Mapear de Entidad a DTO de Respuesta
        return new MesaResponseDTO
        {
            Id = mesaCreada.Id,
            Numero = mesaCreada.Numero,
            Capacidad = mesaCreada.Capacidad,
            Ubicacion = mesaCreada.Ubicacion,
            Estado = mesaCreada.Estado,
            Activa = mesaCreada.Activa
        };
    }

    public async Task<bool> ModificarMesaAsync(Guid id, MesaUpdateDTO mesaDto)
    {
        if (mesaDto == null) throw new ArgumentNullException(nameof(mesaDto));

        // 1. Buscamos la mesa actual usando tu repositorio
        var mesa = await _mesaRepository.GetByIdAsync(id);

        // 2. Verificamos si existe. Si no existe, devolvemos false.
        if (mesa == null)
        {
            return false;
        }

        if (mesaDto.Numero <= 0)
        {
            throw new ValidationException(nameof(mesaDto.Numero), "El número de mesa debe ser mayor a 0.");
        }

        if (mesaDto.Capacidad <= 0)
        {
            throw new ValidationException(nameof(mesaDto.Capacidad), "La capacidad de la mesa debe ser mayor a cero.");
        }

        // RN-03 / RF-02: Validar unicidad de número excluyendo la mesa actual
        if (mesa.Numero != mesaDto.Numero)
        {
            var existeNumero = await _mesaRepository.ExistsByNumeroAsync(mesaDto.Numero, id);
            if (existeNumero)
            {
                throw new ConflictException($"Ya existe una mesa registrada con el número {mesaDto.Numero}.");
            }
        }

        // RN: Si se intenta desactivar, validar que no tenga reservas en curso activas
        if (!mesaDto.Activa && mesa.Activa)
        {
            var tieneReservaEnCurso = await _mesaRepository.HasReservaEnCursoAsync(id);
            if (tieneReservaEnCurso)
            {
                throw new ConflictException("No se puede desactivar una mesa que posee reservas activas en curso.");
            }
        }

        // RN-03 / RF-02: Validar que la nueva capacidad no rompa reservas futuras activas
        if (_reservaRepository != null && mesaDto.Capacidad < mesa.Capacidad)
        {
            var reservas = await _reservaRepository.GetAllAsync(mesaId: id);
            var reservaConflicto = reservas.FirstOrDefault(r =>
                r.FechaHora >= DateTime.UtcNow &&
                r.Estado != ReservaEstado.Cancelada &&
                r.Estado != ReservaEstado.Finalizada &&
                r.Estado != ReservaEstado.NoShow &&
                r.CantidadComensales > mesaDto.Capacidad);

            if (reservaConflicto != null)
            {
                throw new ConflictException(
                    $"No se puede reducir la capacidad a {mesaDto.Capacidad} comensales porque la reserva {reservaConflicto.Id} requiere capacidad para {reservaConflicto.CantidadComensales}.");
            }
        }

        // 3. Modificamos los valores de la mesa
        mesa.Numero = mesaDto.Numero;
        mesa.Capacidad = mesaDto.Capacidad;
        mesa.Activa = mesaDto.Activa;
        mesa.Ubicacion = string.IsNullOrWhiteSpace(mesaDto.Ubicacion) ? "Salón Principal" : mesaDto.Ubicacion.Trim();

        // 4. Guardamos los cambios a través del repositorio
        await _mesaRepository.UpdateAsync(mesa);

        // 5. Retornamos true indicando que todo salió bien
        return true;
    }

    public async Task<bool> EliminarMesaAsync(Guid id)
    {
        // 1. Buscamos si la mesa existe en la base de datos
        var mesa = await _mesaRepository.GetByIdAsync(id);
        if (mesa == null)
        {
            return false; // Retornamos falso si no existe
        }

        // 2. Regla de Negocio: Verificamos si la mesa tiene reservas asignadas
        var tieneReservas = await _mesaRepository.HasReservationsAsync(id);
        if (tieneReservas)
        {
            throw new ConflictException("No se puede eliminar la mesa porque tiene reservas asociadas.");
        }

        // 3. Si todo está bien y no hay reservas, procedemos a borrarla
        await _mesaRepository.DeleteAsync(id);

        // 4. Retornamos true indicando éxito
        return true;
    }

    public async Task<MesaResponseDTO?> CambiarEstadoMesaAsync(Guid id, MesaEstado nuevoEstado)
    {
        var mesa = await _mesaRepository.GetByIdAsync(id);
        if (mesa == null)
        {
            return null;
        }

        // RN-03: Prohibición de marcar Libre si existe reserva en curso
        if (nuevoEstado == MesaEstado.Libre)
        {
            var tieneReservaEnCurso = await _mesaRepository.HasReservaEnCursoAsync(id);
            if (tieneReservaEnCurso)
            {
                throw new MesaConReservaActivaException();
            }
        }

        mesa.Estado = nuevoEstado;
        await _mesaRepository.UpdateAsync(mesa);

        return new MesaResponseDTO
        {
            Id = mesa.Id,
            Numero = mesa.Numero,
            Capacidad = mesa.Capacidad,
            Ubicacion = mesa.Ubicacion,
            Estado = mesa.Estado,
            Activa = mesa.Activa
        };
    }

    public async Task<IEnumerable<MesaResponseDTO>> FiltrarMapaMesasAsync(MesaFiltroDTO filtro)
    {
        // 1. Delegar la consulta filtrada al repositorio
        var mesas = (await _mesaRepository.GetFiltroAsync(
            filtro.Estado,
            filtro.CapacidadMinima,
            filtro.SoloActivas)).ToList();

        // 2. RN: Si no hay resultados con esos filtros, lanzar excepción de negocio
        if (!mesas.Any())
        {
            throw new SinMesasDisponiblesException();
        }

        // 3. Mapear entidades a DTOs de respuesta
        return mesas.Select(m => new MesaResponseDTO
        {
            Id        = m.Id,
            Numero    = m.Numero,
            Capacidad = m.Capacidad,
            Ubicacion = m.Ubicacion,
            Estado    = m.Estado,
            Activa    = m.Activa
        });
    }
}
