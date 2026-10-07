using Shared.Enums;
using Shared.DTOs.Mesas;

namespace BusinessLogic.Mesas.Interfaces;

public interface IMesaService
{
    Task<bool> CambiarEstadoMesaAsync(Guid id, MesaEstado nuevoEstado);
    // Existing methods
    Task<MesaResponseDTO> CreateAsync(MesaCreateDTO dto);
    Task<bool> ModificarMesaAsync(Guid id, MesaUpdateDTO mesaDto);
    Task<bool> EliminarMesaAsync(Guid id);
    // Aquí a futuro podés agregar:
    // Task<MesaResponseDTO?> GetByIdAsync(Guid id);
    // Task<IEnumerable<MesaResponseDTO>> GetAllAsync();
}
