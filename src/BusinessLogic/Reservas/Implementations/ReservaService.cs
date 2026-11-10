using BusinessLogic.Reservas.Interfaces;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Shared.DTOs.Reservas;
using Shared.Enums;
using Shared.Exceptions;
using ClienteEntity = DataAccess.Entities.Cliente;

namespace BusinessLogic.Reservas.Implementations;

public class ReservaService : IReservaService
{
    private readonly IReservaRepository _reservaRepository;
    private readonly IMesaRepository _mesaRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly BusinessLogic.Notifications.Interfaces.INotificationService? _notificationService;
    private const int DuracionEstándarMinutos = 120;

    public ReservaService(
        IReservaRepository reservaRepository,
        IMesaRepository mesaRepository,
        IClienteRepository clienteRepository,
        BusinessLogic.Notifications.Interfaces.INotificationService? notificationService = null)
    {
        _reservaRepository = reservaRepository ?? throw new ArgumentNullException(nameof(reservaRepository));
        _mesaRepository = mesaRepository ?? throw new ArgumentNullException(nameof(mesaRepository));
        _clienteRepository = clienteRepository ?? throw new ArgumentNullException(nameof(clienteRepository));
        _notificationService = notificationService;
    }

    public async Task<ReservaResponseDTO> SolicitarReservaAsync(Guid clienteId, ReservaSolicitudDTO dto)
    {
        // 1. Validar RN-03: Franja horaria operativa (ejemplo: 12:00-16:00 o 20:00-00:00)
        ValidarFranjaHoraria(dto.FechaHora);

        try
        {
            return await _reservaRepository.ExecuteInTransactionAsync(async () =>
            {
                // 2. Validar que el cliente exista
                var cliente = await _clienteRepository.GetByIdAsync(clienteId)
                    ?? throw new NotFoundException(nameof(ClienteEntity), clienteId);

                // 3. Validar reserva activa duplicada del cliente en el mismo turno
                var finReservaSolicitada = dto.FechaHora.AddMinutes(DuracionEstándarMinutos);
                var solapadasCliente = await _reservaRepository.GetOverlappingReservationsByClienteAsync(
                    clienteId,
                    dto.FechaHora,
                    finReservaSolicitada);

                if (solapadasCliente.Any())
                {
                    throw new ReservaDuplicadaClienteException();
                }

                // 4. Validar RN-01 y RN-02: Buscar mesa con capacidad suficiente sin solapamiento
                var todasMesas = await _mesaRepository.GetFiltroAsync(null, dto.CantidadComensales, true);
                var mesasCandidatas = todasMesas
                    .OrderBy(m => m.Capacidad) // Asigna la mesa óptima (menor desperdicio de sillas)
                    .ThenBy(m => m.Numero);

                Mesa? mesaAsignada = null;

                foreach (var mesa in mesasCandidatas)
                {
                    var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
                        mesa.Id,
                        dto.FechaHora,
                        finReservaSolicitada);

                    if (!solapadas.Any())
                    {
                        mesaAsignada = mesa;
                        break;
                    }
                }

                // 5. RN-04: Si no hay mesas libres, disparar excepción para derivar a Lista de Espera
                if (mesaAsignada == null)
                {
                    throw new SinDisponibilidadException();
                }

                // 6. Mapear y Persistir en SQLite (La Despensa)
                var nuevaReserva = new Reserva
                {
                    Id = Guid.NewGuid(),
                    ClienteId = clienteId,
                    MesaId = mesaAsignada.Id,
                    FechaHora = dto.FechaHora,
                    CantidadComensales = dto.CantidadComensales,
                    DuracionEstimadaMinutos = DuracionEstándarMinutos,
                    Estado = ReservaEstado.Pendiente,
                    Observaciones = dto.Observaciones,
                    FechaCreacion = DateTime.UtcNow
                };

                var reservaCreada = await _reservaRepository.CreateAsync(nuevaReserva);

                // 7. Retornar DTO de respuesta
                return MapToResponseDTO(reservaCreada, cliente, mesaAsignada);
            }, System.Data.IsolationLevel.Serializable);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            throw new ConflictException("La mesa seleccionada acaba de ser reservada por otra solicitud concurrente. Por favor, reintente la operación o seleccione otro horario.", ex);
        }
    }

    public async Task<ReservaResponseDTO?> GetByIdAsync(Guid id, Guid? usuarioId = null, string? rol = null)
    {
        var reserva = await _reservaRepository.GetByIdAsync(id);

        if (reserva == null) return null;

        // Validar RN / IDOR: Si el usuario es un Cliente, solo puede consultar sus propias reservas
        if (usuarioId.HasValue && !string.Equals(rol, "Gerente", StringComparison.OrdinalIgnoreCase) 
                               && !string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (reserva.ClienteId != usuarioId.Value)
            {
                throw new AccesoDenegadoException("No posee permisos para consultar una reserva ajena.");
            }
        }

        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> ModificarReservaAsync(Guid reservaId, ReservaUpdateDTO dto, Guid? usuarioId = null, string? rol = null)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        // 1. Validar existencia
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar propiedad (IDOR)
        if (usuarioId.HasValue && !string.Equals(rol, "Gerente", StringComparison.OrdinalIgnoreCase) 
                               && !string.Equals(rol, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (reserva.ClienteId != usuarioId.Value)
            {
                throw new AccesoDenegadoException("No posee permisos para modificar una reserva ajena.");
            }
        }

        // 3. Validar estado (solo Pendiente o Confirmada)
        if (reserva.Estado != ReservaEstado.Pendiente && reserva.Estado != ReservaEstado.Confirmada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible modificar una reserva en estado '{reserva.Estado}'. Solo se admiten reservas Pendientes o Confirmadas.");
        }

        // 4. Validar fecha y franja horaria
        if (dto.FechaHora < DateTime.Now.AddMinutes(-5))
        {
            throw new ValidationException(nameof(dto.FechaHora), "No es posible fijar una reserva para una fecha u hora pasada.");
        }

        ValidarFranjaHoraria(dto.FechaHora);

        // 5. Validar comensales
        if (dto.CantidadComensales <= 0)
        {
            throw new ValidationException(nameof(dto.CantidadComensales), "La cantidad de comensales debe ser mayor a cero.");
        }

        var duracion = dto.DuracionEstimadaMinutos.GetValueOrDefault(reserva.DuracionEstimadaMinutos > 0 ? reserva.DuracionEstimadaMinutos : DuracionEstándarMinutos);
        var finReserva = dto.FechaHora.AddMinutes(duracion);

        // 6. Validar mesa y disponibilidad
        Mesa? mesaAsignada = null;

        if (dto.MesaId.HasValue && dto.MesaId.Value != Guid.Empty)
        {
            var mesaSolicitada = await _mesaRepository.GetByIdAsync(dto.MesaId.Value)
                ?? throw new NotFoundException(nameof(Mesa), dto.MesaId.Value);

            if (!mesaSolicitada.Activa)
            {
                throw new ValidationException(nameof(mesaSolicitada.Activa), "La mesa seleccionada está inactiva.");
            }

            if (mesaSolicitada.Capacidad < dto.CantidadComensales)
            {
                throw new CapacidadInsuficienteException();
            }

            var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
                mesaSolicitada.Id, dto.FechaHora, finReserva, reservaId);

            if (solapadas.Any())
            {
                throw new MesaSolapadaException();
            }

            mesaAsignada = mesaSolicitada;
        }
        else
        {
            // Evaluar si la mesa actual sigue siendo válida para el nuevo horario y comensales
            var mesaActual = reserva.Mesa ?? (reserva.MesaId != Guid.Empty ? await _mesaRepository.GetByIdAsync(reserva.MesaId) : null);

            if (mesaActual != null && mesaActual.Activa && mesaActual.Capacidad >= dto.CantidadComensales)
            {
                var solapadasActual = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
                    mesaActual.Id, dto.FechaHora, finReserva, reservaId);

                if (!solapadasActual.Any())
                {
                    mesaAsignada = mesaActual;
                }
            }

            // Si la mesa actual no sirve o está ocupada en ese horario, buscar automáticamente una mesa disponible
            if (mesaAsignada == null)
            {
                var todasMesas = await _mesaRepository.GetFiltroAsync(null, dto.CantidadComensales, true);
                var candidatas = todasMesas
                    .OrderBy(m => m.Capacidad)
                    .ThenBy(m => m.Numero);

                foreach (var mesa in candidatas)
                {
                    var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
                        mesa.Id, dto.FechaHora, finReserva, reservaId);

                    if (!solapadas.Any())
                    {
                        mesaAsignada = mesa;
                        break;
                    }
                }

                if (mesaAsignada == null)
                {
                    throw new SinDisponibilidadException();
                }
            }
        }

        // 7. Aplicar modificaciones
        reserva.FechaHora = dto.FechaHora;
        reserva.CantidadComensales = dto.CantidadComensales;
        reserva.DuracionEstimadaMinutos = duracion;
        reserva.MesaId = mesaAsignada.Id;
        reserva.Mesa = mesaAsignada;

        if (dto.Observaciones != null)
        {
            reserva.Observaciones = dto.Observaciones;
        }

        // 8. Persistir cambios
        await _reservaRepository.UpdateAsync(reserva);

        return MapToResponseDTO(reserva, reserva.Cliente, mesaAsignada);
    }

    private static void ValidarFranjaHoraria(DateTime fechaHora)
    {
        var hora = fechaHora.TimeOfDay;
        var esTurnoAlmuerzo = hora >= new TimeSpan(12, 0, 0) && hora <= new TimeSpan(15, 30, 0);
        var esTurnoCena = hora >= new TimeSpan(20, 0, 0) && hora <= new TimeSpan(23, 30, 0);

        if (!esTurnoAlmuerzo && !esTurnoCena)
        {
            throw new HorarioNoHabilitadoException();
        }
    }

    private static ReservaResponseDTO MapToResponseDTO(Reserva r, ClienteEntity c, Mesa? m) => new()
    {
        Id = r.Id,
        ClienteId = c.Id,
        ClienteNombreCompleto = $"{c.Nombre} {c.Apellido}",
        ClienteEmail = c.Email,
        ClienteTelefono = c.Telefono,
        MesaId = m?.Id ?? Guid.Empty,
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

    public async Task<ReservaResponseDTO> CancelarReservaClienteAsync(Guid reservaId, Guid clienteId, ReservaCancelarDTO? dto = null)
    {
        // 1. Validar que la reserva exista
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // Validar RN-01: El cliente solo puede cancelar sus propias reservas (Prevenir IDOR)
        if (reserva.ClienteId != clienteId)
        {
            throw new AccesoDenegadoException("No posee permisos para cancelar una reserva ajena.");
        }

        // 2. Verificar RN-02: Solo se pueden cancelar reservas en estado Pendiente o Confirmada
        if (reserva.Estado == ReservaEstado.Cancelada || 
            reserva.Estado == ReservaEstado.EnCurso || 
            reserva.Estado == ReservaEstado.Finalizada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible cancelar una reserva que se encuentra en estado '{reserva.Estado}'.");
        }

        // 3. Aplicar cambios (RN-03: Liberación de la mesa al pasar a estado Cancelada)
        reserva.Estado = ReservaEstado.Cancelada;
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Libre;
            await _mesaRepository.UpdateAsync(reserva.Mesa);
        }

        if (!string.IsNullOrWhiteSpace(dto?.Motivo))
        {
            reserva.Observaciones = string.IsNullOrEmpty(reserva.Observaciones) 
                ? $"[Motivo Cancelación: {dto.Motivo}]" 
                : $"{reserva.Observaciones} | [Motivo Cancelación: {dto.Motivo}]";
        }

        // 4. Persistir cambios
        await _reservaRepository.UpdateAsync(reserva);

        // 5. Retornar DTO con la reserva actualizada
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    } 

    public async Task<IEnumerable<ReservaResponseDTO>> FiltrarPorFechaAsync(Guid clienteId, DateTime fecha)
    {
        var reservas = await _reservaRepository.GetAllAsync(fecha: fecha, clienteId: clienteId);

        return reservas.Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }

    public async Task<IEnumerable<ReservaResponseDTO>> GetListaEsperaAsync()
    {
        // La lista de espera son las reservas en estado Pendiente
        var reservas = await _reservaRepository.GetAllAsync(estado: ReservaEstado.Pendiente);

        return reservas.Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }

    /// <summary>
    /// RF-13 / CU-18: Promueve una reserva en lista de espera asignándole una mesa compatible y notificando al cliente.
    /// </summary>
    public async Task<ReservaResponseDTO> PromoverListaEsperaAsync(Guid reservaId, Guid? mesaId = null)
    {
        var nullNotificationService = _notificationService ?? new BusinessLogic.Notifications.Implementations.NotificationService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BusinessLogic.Notifications.Implementations.NotificationService>.Instance);
        var listaEsperaService = new ListaEsperaService(_reservaRepository, _mesaRepository, _clienteRepository, nullNotificationService);
        return await listaEsperaService.PromoverSolicitudAsync(reservaId, mesaId);
    }
    
    public async Task<ReservaResponseDTO> ConfirmarReservaAsync(Guid reservaId)
    {
        // 1. Validar que la reserva exista
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Verificar regla de negocio: Solo se pueden confirmar reservas Pendientes
        if (reserva.Estado != ReservaEstado.Pendiente)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible confirmar una reserva que se encuentra en estado '{reserva.Estado}'. Solo se admiten reservas Pendientes.");
        }

        // 3. RN-03 / CU-12: Revalidar disponibilidad de mesa, capacidad y ausencia de solapamiento
        if (reserva.MesaId == Guid.Empty)
        {
            throw new ConflictException("La reserva no tiene una mesa asignada para poder confirmarse.");
        }

        var mesa = reserva.Mesa ?? await _mesaRepository.GetByIdAsync(reserva.MesaId);
        if (mesa == null || !mesa.Activa || mesa.Estado == MesaEstado.EnMantenimiento)
        {
            throw new MesaNoCompatibleException("La mesa asignada no se encuentra disponible o activa.");
        }

        if (mesa.Capacidad < reserva.CantidadComensales)
        {
            throw new CapacidadInsuficienteException();
        }

        var finReserva = reserva.FechaHora.AddMinutes(reserva.DuracionEstimadaMinutos);
        var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
            mesa.Id,
            reserva.FechaHora,
            finReserva,
            reserva.Id);

        if (solapadas.Any())
        {
            throw new MesaSolapadaException();
        }

        // 4. Aplicar cambios
        reserva.Estado = ReservaEstado.Confirmada;
        reserva.Mesa = mesa;

        // 5. Persistir cambios
        await _reservaRepository.UpdateAsync(reserva);

        // 6. Retornar DTO
        return MapToResponseDTO(reserva, reserva.Cliente, mesa);
    }

    public async Task<ReservaResponseDTO> IniciarReservaAsync(Guid reservaId)
    {
        // 1. Buscar la reserva incluyendo la entidad Mesa (necesitamos actualizarla)
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar RN-01: Solo se pueden iniciar reservas en estado Confirmada
        if (reserva.Estado != ReservaEstado.Confirmada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible iniciar una reserva que se encuentra en estado '{reserva.Estado}'. La reserva debe estar 'Confirmada' previamente.");
        }

        // 3. Aplicar RN-03: Cambiar estado de la reserva y de la mesa asignada
        reserva.Estado = ReservaEstado.EnCurso;
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Ocupada;
            await _mesaRepository.UpdateAsync(reserva.Mesa);
        }

        // 4. Persistir cambios
        await _reservaRepository.UpdateAsync(reserva);

        // 5. Retornar DTO con los datos actualizados
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> FinalizarReservaAsync(Guid reservaId)
    {
        // 1. Buscar la reserva incluyendo la entidad Mesa para poder liberarla
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar RN-01: Solo se pueden finalizar reservas que están en curso
        if (reserva.Estado != ReservaEstado.EnCurso)
        {
            throw new TransicionEstadoInvalidaException(
                $"Solo es posible finalizar reservas que se encuentren en curso. Estado actual: '{reserva.Estado}'.");
        }

        // 3. Aplicar RN-02: Finalizar la reserva y LIBERAR la mesa
        reserva.Estado = ReservaEstado.Finalizada;
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Libre;
            await _mesaRepository.UpdateAsync(reserva.Mesa);
        }

        // 4. Guardar cambios en base de datos
        await _reservaRepository.UpdateAsync(reserva);

        // 5. Retornar DTO con el estado final
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> CambiarEstadoAsync(Guid reservaId, ReservaCambiarEstadoDTO dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        if (reserva.Estado == dto.NuevoEstado)
        {
            return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
        }

        // Validación de máquina de estados de reserva (RF-10)
        bool esValida = (reserva.Estado, dto.NuevoEstado) switch
        {
            (ReservaEstado.Pendiente, ReservaEstado.Confirmada) => true,
            (ReservaEstado.Pendiente, ReservaEstado.Cancelada) => true,
            (ReservaEstado.Confirmada, ReservaEstado.EnCurso) => true,
            (ReservaEstado.Confirmada, ReservaEstado.Cancelada) => true,
            (ReservaEstado.Confirmada, ReservaEstado.NoShow) => true,
            (ReservaEstado.EnCurso, ReservaEstado.Finalizada) => true,
            (ReservaEstado.EnCurso, ReservaEstado.Cancelada) => true,
            _ => false
        };

        if (!esValida)
        {
            throw new TransicionEstadoInvalidaException(
                $"No está permitida la transición de estado de '{reserva.Estado}' a '{dto.NuevoEstado}'.");
        }

        // Validar mesa si pasa a confirmada
        var mesa = reserva.Mesa ?? (reserva.MesaId != Guid.Empty ? await _mesaRepository.GetByIdAsync(reserva.MesaId) : null);
        if (dto.NuevoEstado == ReservaEstado.Confirmada)
        {
            if (mesa != null && !mesa.Activa)
            {
                throw new ValidationException(nameof(mesa.Activa), "La mesa asignada a la reserva está inactiva.");
            }
        }

        reserva.Estado = dto.NuevoEstado;

        // Actualizar estado de la mesa vinculada según la transición
        if (mesa != null)
        {
            if (dto.NuevoEstado == ReservaEstado.EnCurso)
            {
                mesa.Estado = MesaEstado.Ocupada;
                await _mesaRepository.UpdateAsync(mesa);
            }
            else if (dto.NuevoEstado == ReservaEstado.Finalizada || 
                     dto.NuevoEstado == ReservaEstado.Cancelada || 
                     dto.NuevoEstado == ReservaEstado.NoShow)
            {
                mesa.Estado = MesaEstado.Libre;
                await _mesaRepository.UpdateAsync(mesa);
            }
        }

        await _reservaRepository.UpdateAsync(reserva);
        return MapToResponseDTO(reserva, reserva.Cliente, mesa);
    }
    
    public async Task<ReservaResponseDTO> CancelarReservaGerenteAsync(Guid reservaId, ReservaCancelarDTO dto)
    {
        // CU-15: el motivo es obligatorio para cancelaciones del gerente
        if (string.IsNullOrWhiteSpace(dto?.Motivo))
            throw new ValidationException("El motivo de cancelación es obligatorio para el gerente.");

        // 1. Buscar la reserva incluyendo la Mesa (necesitamos liberarla)
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar que la reserva NO esté ya en un estado final (no tiene sentido cancelar algo ya concluido)
        if (reserva.Estado == ReservaEstado.Cancelada ||
            reserva.Estado == ReservaEstado.Finalizada ||
            reserva.Estado == ReservaEstado.NoShow ||
            reserva.Estado == ReservaEstado.Rechazada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible cancelar una reserva que ya se encuentra en estado '{reserva.Estado}'.");
        }

        // 3. Cambiar estado de la reserva
        reserva.Estado = ReservaEstado.Cancelada;

        // 4. Persistir motivo en Observaciones
        reserva.Observaciones = string.IsNullOrEmpty(reserva.Observaciones)
            ? $"[Motivo Cancelación Gerente: {dto.Motivo}]"
            : $"{reserva.Observaciones} | [Motivo Cancelación Gerente: {dto.Motivo}]";

        // 5. Liberar la mesa inmediatamente (RN-03)
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Libre;
            await _mesaRepository.UpdateAsync(reserva.Mesa);
        }

        // 6. Persistir los cambios
        await _reservaRepository.UpdateAsync(reserva);

        // 7. Retornar DTO con el estado actualizado
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> RechazarReservaAsync(Guid reservaId, ReservaRechazarDTO dto)
    {
        // CU-16: el motivo de rechazo es obligatorio
        if (string.IsNullOrWhiteSpace(dto?.MotivoRechazo))
            throw new ValidationException("El motivo de rechazo es obligatorio.");

        // 1. Buscar la reserva
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar RN-02: Solo se pueden rechazar reservas en estado Pendiente
        if (reserva.Estado != ReservaEstado.Pendiente)
        {
            throw new TransicionEstadoInvalidaException(
                $"Solo pueden rechazarse reservas en estado Pendiente. Estado actual: '{reserva.Estado}'.");
        }

        // 3. Cambiar estado a Rechazada
        reserva.Estado = ReservaEstado.Rechazada;

        // 4. Persistir motivo en Observaciones
        reserva.Observaciones = string.IsNullOrEmpty(reserva.Observaciones)
            ? $"[Motivo Rechazo: {dto.MotivoRechazo}]"
            : $"{reserva.Observaciones} | [Motivo Rechazo: {dto.MotivoRechazo}]";

        // 5. Liberar la mesa si tenía asignada (RN-03)
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Libre;
            await _mesaRepository.UpdateAsync(reserva.Mesa);
        }

        // 6. Persistir los cambios
        await _reservaRepository.UpdateAsync(reserva);

        // 7. Retornar DTO
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> AsignarMesaAsync(Guid reservaId, Guid mesaId)
    {
        // 1. Validar que la reserva exista
        var reserva = await _reservaRepository.GetByIdAsync(reservaId)
            ?? throw new NotFoundException(nameof(Reserva), reservaId);

        // 2. Validar que la mesa exista por ID
        var mesa = await _mesaRepository.GetByIdAsync(mesaId)
            ?? throw new NotFoundException(nameof(Mesa), mesaId);

        // 3. Validar estado de la reserva (no se puede cambiar mesa a una reserva ya finalizada o cancelada)
        if (reserva.Estado == ReservaEstado.Finalizada || reserva.Estado == ReservaEstado.Cancelada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No se puede reasignar mesa a una reserva en estado '{reserva.Estado}'.");
        }

        // 4. Validar estado de la mesa (debe estar activa y no en mantenimiento)
        if (!mesa.Activa)
        {
            throw new ValidationException(nameof(mesa.Activa), "La mesa seleccionada está inactiva.");
        }

        if (mesa.Estado == MesaEstado.EnMantenimiento)
        {
            throw new ValidationException(nameof(mesa.Estado), "La mesa seleccionada se encuentra en mantenimiento.");
        }

        // 5. RN-01: Validar capacidad
        if (mesa.Capacidad < reserva.CantidadComensales)
        {
            throw new CapacidadInsuficienteException();
        }

        // 6. RN-02: Validar solapamiento de horario
        var finReserva = reserva.FechaHora.AddMinutes(reserva.DuracionEstimadaMinutos);

        var solapadas = await _reservaRepository.GetOverlappingReservationsByMesaAsync(
            mesa.Id,
            reserva.FechaHora,
            finReserva,
            reservaId);

        if (solapadas.Any())
        {
            throw new MesaSolapadaException();
        }

        // 7. Actualizar mesa y guardar
        reserva.MesaId = mesa.Id;
        reserva.Mesa = mesa;

        await _reservaRepository.UpdateAsync(reserva);

        return MapToResponseDTO(reserva, reserva.Cliente, mesa);
    }

    public async Task<IEnumerable<ReservaResponseDTO>> GetTableroOperativoAsync(DateTime? fecha = null)
    {
        var fechaConsulta = fecha?.Date ?? DateTime.Today;

        var reservas = await _reservaRepository.GetReservasOperativasAsync(fechaConsulta);

        if (!reservas.Any())
        {
            throw new SinReservasOperativasException();
        }

        return reservas.Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }

    /// CU-11: Filtrar reservas (Gerente).
    public async Task<IEnumerable<ReservaResponseDTO>> FiltrarReservasAsync(ReservaFiltroDTO filtro)
    {
        var reservas = await _reservaRepository.GetAllAsync(
            filtro.Fecha,
            filtro.ClienteId,
            filtro.MesaId,
            filtro.Estado);

        return reservas.Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }
}
