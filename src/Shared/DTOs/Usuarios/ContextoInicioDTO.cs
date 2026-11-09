using Shared.DTOs.Reservas;

namespace Shared.DTOs.Usuarios;

public class ContextoInicioDTO
{
    public Guid UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string MensajeBienvenida { get; set; } = string.Empty;
    public List<string> AccesosRapidos { get; set; } = new();

    // Resumen según el rol del usuario (RN-01 de CU-09)
    public ResumenClienteDTO? ResumenCliente { get; set; }
    public ResumenGerenteDTO? ResumenGerente { get; set; }
    public ResumenAdminDTO? ResumenAdmin { get; set; }
}

public class ResumenClienteDTO
{
    public int TotalReservasActivas { get; set; }
    public List<ReservaResponseDTO> ProximasReservas { get; set; } = new();
}

public class ResumenGerenteDTO
{
    public int TotalMesas { get; set; }
    public int MesasLibres { get; set; }
    public int MesasOcupadas { get; set; }
    public int ReservasHoy { get; set; }
    public int ReservasPendientesListaEspera { get; set; }
}

public class ResumenAdminDTO
{
    public int TotalUsuarios { get; set; }
    public int AdminsActivos { get; set; }
    public int TotalMesas { get; set; }
    public int TotalReservasHoy { get; set; }
}

