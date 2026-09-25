using Shared.DTOs.Clientes;

namespace BusinessLogic.Cliente.Interfaces;

public interface IClienteService
{
    Task<IEnumerable<ClienteResponseDTO>> GetAllAsync();
    Task<ClienteResponseDTO?> GetByIdAsync(Guid id);
    Task<ClienteResponseDTO> CreateAsync(ClienteCreateDTO dto);
}
