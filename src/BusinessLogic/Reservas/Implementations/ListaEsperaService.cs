using BusinessLogic.Notifications.Interfaces;
using BusinessLogic.Reservas.Interfaces;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Reservas;
using Shared.Enums;
using Shared.Exceptions;
using ClienteEntity = DataAccess.Entities.Cliente;

namespace BusinessLogic.Reservas.Implementations;

public class ListaEsperaService : IListaEsperaService
{
    private readonly IReservaRepository _reservaRepository;
    private readonly IMesaRepository _mesaRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly INotificationService _notificationService;
    private const int DuracionEstandarMinutos = 120;

    public ListaEsperaService(
        IReservaRepository reservaRepository,
        IMesaRepository mesaRepository,
        IClienteRepository clienteRepository,
        INotificationService notificationService)
    {
        _reservaRepository = reservaRepository ?? throw new ArgumentNullException(nameof(reservaRepository));
        _mesaRepository = mesaRepository ?? throw new ArgumentNullException(nameof(mesaRepository));
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ReservaResponseDTO>> GetSolicitudesOrdenadasAsync()
    {
        // RN-02: Recuperar solicitudes activas ordenadas por orden de llegada (FIFO)
        var reservas = await _reservaRepository.GetAllAsync(estado: ReservaEstado.Pendiente);

        return reservas
            .OrderBy(r => r.FechaCreacion)
            .ThenBy(r => r.FechaHora)
            .Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }

    /// <inheritdoc />
    public async Task<ReservaResponseDTO> PromoverSolicitudAsync(Guid reservaId, Guid? mesaId = null)
    {
        // 1. Validar existencia de la solicitud (Doc 4a: 404 Not Found)
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar que se encuentre en estado Pendiente (Lista de espera)
        if (reserva.Estado != ReservaEstado.Pendiente)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible promover una reserva en estado '{reserva.Estado}'. Solo se admiten solicitudes en estado Pendiente.");
        }

        var duracion = reserva.DuracionEstimadaMinutos > 0 ? reserva.DuracionEstimadaMinutos : DuracionEstandarMinutos;
        var finReserva = reserva.FechaHora.AddMinutes(duracion);

        Mesa? mesaAsignada = null;

        // 3. RN-03: Asignación manual o automática de mesa compatible y libre
        if (mesaId.HasValue && mesaId.Value != Guid.Empty)
        {
            // Asignación manual propuesta por el Gerente
            var mesaPropuesta = await _mesaRepository.GetByIdAsync(mesaId.Value)
                ?? throw new MesaNoEncontradaException(mesaId.Value);

            if (!mesaPropuesta.Activa)
            {
                throw new MesaNoCompatibleException("La mesa propuesta se encuentra inactiva.");
            }

            if (mesaPropuesta.Capacidad < reserva.CantidadComensales)
            {
                throw new MesaNoCompatibleException(
                    $"La mesa #{mesaPropuesta.Numero} tiene capacidad ({mesaPropuesta.Capacidad}) insuficiente para {reserva.CantidadComensales} comensales.");
            }

            var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
                mesaPropuesta.Id,
                reserva.FechaHora,
                finReserva,
                reserva.Id);

            if (solapadas.Any())
            {
                throw new MesaNoCompatibleException(
                    $"La mesa #{mesaPropuesta.Numero} no está disponible en la franja horaria requerida.");
            }

            mesaAsignada = mesaPropuesta;
        }
        else
        {
            // Asignación automática: mesa óptima (menor desperdicio de sillas) sin solapamiento
            var todasMesas = await _mesaRepository.GetFiltroAsync(null, reserva.CantidadComensales, true);
            var candidatas = todasMesas
                .OrderBy(m => m.Capacidad)
                .ThenBy(m => m.Numero);

            foreach (var mesa in candidatas)
            {
                var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
                    mesa.Id,
                    reserva.FechaHora,
                    finReserva,
                    reserva.Id);

                if (!solapadas.Any())
                {
                    mesaAsignada = mesa;
                    break;
                }
            }

            if (mesaAsignada == null)
            {
                throw new MesaNoCompatibleException(
                    "No se encontró ninguna mesa disponible con capacidad compatible para la fecha y hora de la solicitud.");
            }
        }

        // 4. Promover solicitud a estado Confirmada y asignar mesa
        reserva.MesaId = mesaAsignada.Id;
        reserva.Mesa = mesaAsignada;
        reserva.Estado = ReservaEstado.Confirmada;

        await _reservaRepository.UpdateAsync(reserva);

        // 5. Cargar datos de cliente para notificación y respuesta
        var cliente = reserva.Cliente ?? await _clienteRepository.GetByIdAsync(reserva.ClienteId);

        // 6. Notificar al cliente de la asignación de su mesa
        if (cliente != null)
        {
            await _notificationService.NotificarAsignacionMesaAsync(
                cliente.Id,
                cliente.Email,
                $"{cliente.Nombre} {cliente.Apellido}".Trim(),
                mesaAsignada.Numero,
                reserva.FechaHora);
        }

        return MapToResponseDTO(reserva, cliente, mesaAsignada);
    }

    private static ReservaResponseDTO MapToResponseDTO(Reserva r, ClienteEntity? c, Mesa? m) => new()
    {
        Id = r.Id,
        ClienteId = c?.Id ?? r.ClienteId,
        ClienteNombreCompleto = c != null ? $"{c.Nombre} {c.Apellido}".Trim() : string.Empty,
        ClienteEmail = c?.Email ?? string.Empty,
        ClienteTelefono = c?.Telefono ?? string.Empty,
        MesaId = m?.Id ?? r.MesaId,
        MesaNumero = m?.Numero ?? 0,
        MesaCapacidad = m?.Capacidad ?? 0,
        MesaUbicacion = m?.Ubicacion ?? string.Empty,
        FechaHora = r.FechaHora,
        CantidadComensales = r.CantidadComensales,
        DuracionEstimadaMinutos = r.DuracionEstimadaMinutos,
        Estado = r.Estado,
        Observaciones = r.Observaciones,
        FechaCreacion = r.FechaCreacion
    };
}

