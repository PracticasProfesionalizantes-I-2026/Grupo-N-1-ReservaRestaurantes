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

    public MesaService(IMesaRepository mesaRepository)
    {
        _mesaRepository = mesaRepository ?? throw new ArgumentNullException(nameof(mesaRepository));
    }

    public async Task<MesaResponseDTO> CreateAsync(MesaCreateDTO dto)
    {
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
            Capacidad = dto.Capacidad
            // Estado y Activa ya tienen valores por defecto (Libre y true) en la entidad
        };

        // 3. Persistir en la base de datos
        var mesaCreada = await _mesaRepository.CreateAsync(nuevaMesa);

        // 4. Mapear de Entidad a DTO de Respuesta
        return new MesaResponseDTO
        {
            Id = mesaCreada.Id,
            Numero = mesaCreada.Numero,
            Capacidad = mesaCreada.Capacidad,
            Estado = mesaCreada.Estado,
            Activa = mesaCreada.Activa
        };
    }

    public async Task<bool> ModificarMesaAsync(Guid id, MesaUpdateDTO mesaDto)
    {
        // 1. Buscamos la mesa actual usando tu repositorio
        var mesa = await _mesaRepository.GetByIdAsync(id);

        // 2. Verificamos si existe. Si no existe, devolvemos false.
        if (mesa == null)
        {
            return false;
        }

        // 3. Modificamos los valores de la mesa
        mesa.Capacidad = mesaDto.Capacidad;
        mesa.Activa = mesaDto.Activa;

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
            Estado = mesa.Estado,
            Activa = mesa.Activa
        };
    }
}
