using Shared.Enums;
using Shared.DTOs.Mesas;

namespace BusinessLogic.Mesas.Interfaces;

public interface IMesaService
{
    Task<MesaResponseDTO?> CambiarEstadoMesaAsync(Guid id, MesaEstado nuevoEstado);
    Task<MesaResponseDTO> CreateAsync(MesaCreateDTO dto);
    Task<bool> ModificarMesaAsync(Guid id, MesaUpdateDTO mesaDto);
    Task<bool> EliminarMesaAsync(Guid id);
    Task<IEnumerable<MesaResponseDTO>> FiltrarMapaMesasAsync(MesaFiltroDTO filtro);

}
