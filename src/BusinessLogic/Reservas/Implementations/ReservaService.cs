using BusinessLogic.Reservas.Interfaces;
using DataAccess.Context;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Reservas;
using Shared.Enums;
using Shared.Exceptions;
using ClienteEntity = DataAccess.Entities.Cliente;

namespace BusinessLogic.Reservas.Implementations;

public class ReservaService : IReservaService
{
    private readonly RestaurantDbContext _context;
    private const int DuracionEstándarMinutos = 120;

    public ReservaService(RestaurantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ReservaResponseDTO> SolicitarReservaAsync(Guid clienteId, ReservaSolicitudDTO dto)
    {
        // 1. Validar RN-03: Franja horaria operativa (ejemplo: 12:00-16:00 o 20:00-00:00)
        ValidarFranjaHoraria(dto.FechaHora);

        // 2. Validar que el cliente exista
        var cliente = await _context.Clientes.FindAsync(clienteId)
            ?? throw new NotFoundException(nameof(ClienteEntity), clienteId);

        // 3. Validar reserva activa duplicada del cliente en el mismo turno
        var finReservaSolicitada = dto.FechaHora.AddMinutes(DuracionEstándarMinutos);
        var tieneReservaActiva = await _context.Reservas
            .AnyAsync(r => r.ClienteId == clienteId &&
                           r.Estado != ReservaEstado.Cancelada &&
                           r.Estado != ReservaEstado.Finalizada &&
                           r.FechaHora < finReservaSolicitada &&
                           r.FechaHora.AddMinutes(r.DuracionEstimadaMinutos) > dto.FechaHora);

        if (tieneReservaActiva)
        {
            throw new ReservaDuplicadaClienteException();
        }

        // 4. Validar RN-01 y RN-02: Buscar mesa con capacidad suficiente sin solapamiento
        var mesasCandidatas = await _context.Mesas
            .Where(m => m.Activa && m.Capacidad >= dto.CantidadComensales)
            .OrderBy(m => m.Capacidad) // Asigna la mesa óptima (menor desperdicio de sillas)
            .ToListAsync();

        Mesa? mesaAsignada = null;

        foreach (var mesa in mesasCandidatas)
        {
            var solapada = await _context.Reservas
                .AnyAsync(r => r.MesaId == mesa.Id &&
                               r.Estado != ReservaEstado.Cancelada &&
                               r.FechaHora < finReservaSolicitada &&
                               r.FechaHora.AddMinutes(r.DuracionEstimadaMinutos) > dto.FechaHora);

            if (!solapada)
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

        await _context.Reservas.AddAsync(nuevaReserva);
        await _context.SaveChangesAsync();

        // 7. Retornar DTO de respuesta
        return MapToResponseDTO(nuevaReserva, cliente, mesaAsignada);
    }

    public async Task<ReservaResponseDTO?> GetByIdAsync(Guid id)
    {
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reserva == null) return null;

        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
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

    private static ReservaResponseDTO MapToResponseDTO(Reserva r, ClienteEntity c, Mesa m) => new()
    {
        Id = r.Id,
        ClienteId = c.Id,
        ClienteNombreCompleto = $"{c.Nombre} {c.Apellido}",
        ClienteEmail = c.Email,
        ClienteTelefono = c.Telefono,
        MesaId = m.Id,
        MesaNumero = m.Numero,
        MesaCapacidad = m.Capacidad,
        MesaUbicacion = m.Ubicacion,
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
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
         ?? throw new 
         ReservaNoEncontradaException(reservaId);
        
        // 3. Verificar RN-02: Solo se pueden cancelar reservas en estado Pendiente o Confirmada

        if (reserva.Estado == ReservaEstado.Cancelada || 
            reserva.Estado == ReservaEstado.EnCurso || 
            reserva.Estado == ReservaEstado.Finalizada)
    {
        throw new TransicionEstadoInvalidaException(
            $"No es posible cancelar una reserva que se encuentra en estado '{reserva.Estado}'.");
    }

    // 4. Aplicar cambios (RN-03: Liberación de la mesa al pasar a estado Cancelada)

        reserva.Estado = ReservaEstado.Cancelada;
        if (!string.IsNullOrWhiteSpace(dto?.Motivo))
        {
         reserva.Observaciones = string.IsNullOrEmpty(reserva.Observaciones) 
             ? $"[Motivo Cancelación: {dto.Motivo}]" 
             : $"{reserva.Observaciones} | [Motivo Cancelación: {dto.Motivo}]";
        }
    // 5. Persistir cambios
        await _context.SaveChangesAsync();
    // 6. Retornar DTO con la reserva actualizada
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    } 

    public async Task<IEnumerable<ReservaResponseDTO>> FiltrarPorFechaAsync(Guid clienteId, DateTime fecha)
    {
        var fechaBuscada = fecha.Date;
        
        var reservas = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .Where(r => r.ClienteId == clienteId && r.FechaHora.Date == fechaBuscada)
            .OrderBy(r => r.FechaHora)
            .ToListAsync();

        return reservas.Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }

    public async Task<IEnumerable<ReservaResponseDTO>> GetListaEsperaAsync()
    {
        // El usuario definió que la lista de espera son simplemente las reservas en estado Pendiente
        var reservas = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .Where(r => r.Estado == ReservaEstado.Pendiente)
            .OrderBy(r => r.FechaHora)
            .ThenBy(r => r.FechaCreacion)
            .ToListAsync();

        if (!reservas.Any())
        {
            throw new ListaEsperaVaciaException();
        }

        return reservas.Select(r => MapToResponseDTO(r, r.Cliente, r.Mesa));
    }
    
    public async Task<ReservaResponseDTO> ConfirmarReservaAsync(Guid reservaId)
    {
        // 1. Validar que la reserva exista
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Verificar regla de negocio: Solo se pueden confirmar reservas Pendientes
        if (reserva.Estado != ReservaEstado.Pendiente)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible confirmar una reserva que se encuentra en estado '{reserva.Estado}'. Solo se admiten reservas Pendientes.");
        }

        // 3. Aplicar cambios
        reserva.Estado = ReservaEstado.Confirmada;

        // 4. Persistir cambios
        await _context.SaveChangesAsync();

        // 5. Retornar DTO
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    
    public async Task<ReservaResponseDTO> IniciarReservaAsync(Guid reservaId)
    {
        // 1. Buscar la reserva incluyendo la entidad Mesa (necesitamos actualizarla)
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar RN-01: Solo se pueden iniciar reservas en estado Confirmada
        if (reserva.Estado != ReservaEstado.Confirmada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible iniciar una reserva que se encuentra en estado '{reserva.Estado}'. La reserva debe estar 'Confirmada' previamente.");
        }

        // 3. Aplicar RN-03: Cambiar estado de la reserva y de la mesa asignada
        reserva.Estado = ReservaEstado.EnCurso;
        reserva.Mesa.Estado = MesaEstado.Ocupada;

        // 4. Persistir cambios en una única transacción automática de EF Core
        await _context.SaveChangesAsync();

        // 5. Retornar DTO con los datos actualizados
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> FinalizarReservaAsync(Guid reservaId)
    {
        // 1. Buscar la reserva incluyendo la entidad Mesa para poder liberarla
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar RN-01: Solo se pueden finalizar reservas que están en curso
        if (reserva.Estado != ReservaEstado.EnCurso)
        {
            throw new TransicionEstadoInvalidaException(
                $"Solo es posible finalizar reservas que se encuentren en curso. Estado actual: '{reserva.Estado}'.");
        }

        // 3. Aplicar RN-02: Finalizar la reserva y LIBERAR la mesa
        reserva.Estado = ReservaEstado.Finalizada;
        reserva.Mesa.Estado = MesaEstado.Libre;

        // 4. Guardar cambios en base de datos
        await _context.SaveChangesAsync();

        // 5. Retornar DTO con el estado final
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }
    
    public async Task<ReservaResponseDTO> CancelarReservaGerenteAsync(Guid reservaId)
    {
        // 1. Buscar la reserva incluyendo la Mesa (necesitamos liberarla)
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar que la reserva NO esté ya en un estado final (no tiene sentido cancelar algo ya concluido)
        if (reserva.Estado == ReservaEstado.Cancelada ||
            reserva.Estado == ReservaEstado.Finalizada ||
            reserva.Estado == ReservaEstado.NoShow)
        {
            throw new TransicionEstadoInvalidaException(
                $"No es posible cancelar una reserva que ya se encuentra en estado '{reserva.Estado}'.");
        }

        // 3. Cambiar estado de la reserva
        reserva.Estado = ReservaEstado.Cancelada;

        // 4. Liberar la mesa inmediatamente (RN-03)
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Libre;
        }

        // 5. Persistir los cambios
        await _context.SaveChangesAsync();

        // 6. Retornar DTO con el estado actualizado
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

    public async Task<ReservaResponseDTO> RechazarReservaAsync(Guid reservaId)
    {
        // 1. Buscar la reserva
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
            ?? throw new ReservaNoEncontradaException(reservaId);

        // 2. Validar RN-02: Solo se pueden rechazar reservas en estado Pendiente
        if (reserva.Estado != ReservaEstado.Pendiente)
        {
            throw new TransicionEstadoInvalidaException(
                $"Solo pueden rechazarse reservas en estado Pendiente. Estado actual: '{reserva.Estado}'.");
        }

        // 3. Cambiar estado a Cancelada ya que no existe estado rechazada
        reserva.Estado = ReservaEstado.Cancelada;

        // 4. Liberar la mesa 
        if (reserva.Mesa != null)
        {
            reserva.Mesa.Estado = MesaEstado.Libre;
        }

        // 5. Persistir los cambios
        await _context.SaveChangesAsync();

        // 6. Retornar DTO
        return MapToResponseDTO(reserva, reserva.Cliente, reserva.Mesa);
    }

        public async Task<ReservaResponseDTO> AsignarMesaAsync(Guid reservaId, int mesaId)
    {
        // 1. Validar que la reserva exista
        var reserva = await _context.Reservas
            .Include(r => r.Cliente)
            .Include(r => r.Mesa)
            .FirstOrDefaultAsync(r => r.Id == reservaId)
            ?? throw new NotFoundException(nameof(Reserva), reservaId);

        // 2. Validar que la mesa exista
        var mesa = await _context.Mesas
            .FindAsync(mesaId)
            ?? throw new NotFoundException(nameof(Mesa), mesaId);

        // 3. Validar estado (no se puede cambiar mesa a una reserva ya finalizada o cancelada)
        if (reserva.Estado == ReservaEstado.Finalizada || reserva.Estado == ReservaEstado.Cancelada)
        {
            throw new TransicionEstadoInvalidaException(
                $"No se puede reasignar mesa a una reserva en estado '{reserva.Estado}'.");
        }

        // 4. RN-01: Validar capacidad
        if (mesa.Capacidad < reserva.CantidadComensales)
        {
            throw new CapacidadInsuficienteException();
        }

        // 5. RN-02: Validar solapamiento de horario
        var finReserva = reserva.FechaHora.AddMinutes(reserva.DuracionEstimadaMinutos);

        var solapada = await _context.Reservas
            .AnyAsync(r => r.MesaId == mesa.Id &&
                           r.Id != reservaId &&
                           r.Estado != ReservaEstado.Cancelada &&
                           r.Estado != ReservaEstado.Finalizada &&
                           r.FechaHora < finReserva &&
                           r.FechaHora.AddMinutes(r.DuracionEstimadaMinutos) > reserva.FechaHora);

        if (solapada)
        {
            throw new MesaSolapadaException();
        }

        // 6. Actualizar mesa y guardar
        reserva.MesaId = mesa.Id;
        reserva.Mesa = mesa;

        await _context.SaveChangesAsync();

        return MapToResponseDTO(reserva, reserva.Cliente, mesa);
    }



}
