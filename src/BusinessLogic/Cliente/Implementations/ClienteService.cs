using BusinessLogic.Cliente.Interfaces;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Clientes;
using Shared.Exceptions;
using EntityCliente = DataAccess.Entities.Cliente;

namespace BusinessLogic.Cliente.Implementations;

public class ClienteService : IClienteService
{
    private readonly IClienteRepository _clienteRepository;

    public ClienteService(IClienteRepository clienteRepository)
    {
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
    }

    public async Task<ClienteResponseDTO> CreateAsync(ClienteCreateDTO dto)
    {
        // 1. Normalización de datos
        var emailNormalized = dto.Email.Trim().ToLower();

        // 2. Verificación de Regla de Negocio RN-01 (Email Único)
        bool emailExiste = await _clienteRepository.ExistsByEmailAsync(emailNormalized);
        if (emailExiste)
        {
            throw new ConflictException("Cliente", "Email", dto.Email);
        }

        // 3. Mapeo DTO -> Entidad
        var clienteEntity = new EntityCliente
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = emailNormalized,
            Telefono = dto.Telefono.Trim(),
            FechaRegistro = DateTime.UtcNow
        };

        // 4. Persistencia en Base de Datos
        var clienteCreado = await _clienteRepository.CreateAsync(clienteEntity);

        // 5. Mapeo Entidad -> DTO de Respuesta
        return MapToResponseDTO(clienteCreado);
    }

    public async Task<IEnumerable<ClienteResponseDTO>> GetAllAsync()
    {
        var clientes = await _clienteRepository.GetAllAsync();
        return clientes.Select(MapToResponseDTO);
    }

    public async Task<ClienteResponseDTO?> GetByIdAsync(Guid id)
    {
        var cliente = await _clienteRepository.GetByIdAsync(id);
        return cliente == null ? null : MapToResponseDTO(cliente);
    }

    private static ClienteResponseDTO MapToResponseDTO(EntityCliente entity)
    {
        return new ClienteResponseDTO
        {
            Id = entity.Id,
            Nombre = entity.Nombre,
            Apellido = entity.Apellido,
            Email = entity.Email,
            Telefono = entity.Telefono,
            FechaRegistro = entity.FechaRegistro
        };
    }
}
