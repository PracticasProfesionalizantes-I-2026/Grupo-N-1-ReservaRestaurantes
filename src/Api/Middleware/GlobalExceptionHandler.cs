using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shared.Exceptions;

namespace Api.Middleware;

/// <summary>
/// Middleware global centralizado para el manejo de excepciones.
/// Intercepta todas las excepciones no controladas y las convierte en
/// respuestas HTTP estructuradas con el formato ProblemDetails (RFC 7807).
/// Esto elimina la necesidad de bloques try/catch en los controladores.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Mapeo centralizado: tipo de excepción de negocio → status code HTTP + título
        // Nota: Las clases derivadas deben evaluarse antes de sus clases base.
        var (statusCode, title) = exception switch
        {
            // 400 Bad Request — errores de validación y reglas de formato
            ValidationException                 => (StatusCodes.Status400BadRequest,   "Validación Fallida"),
            HorarioNoHabilitadoException        => (StatusCodes.Status400BadRequest,   "Horario No Habilitado"),
            ArgumentException                   => (StatusCodes.Status400BadRequest,   "Argumento Inválido"),

            // 401 Unauthorized — acceso denegado por credenciales o sesión
            UnauthorizedException               => (StatusCodes.Status401Unauthorized, "No Autorizado"),

            // 404 Not Found — recursos inexistentes (subclases antes de NotFoundException base)
            ReservaNoEncontradaException        => (StatusCodes.Status404NotFound,     "Reserva No Encontrada"),
            MesaNoEncontradaException           => (StatusCodes.Status404NotFound,     "Mesa No Encontrada"),
            SinMesasDisponiblesException        => (StatusCodes.Status404NotFound,     "Sin Mesas Disponibles"),
            SinReservasOperativasException      => (StatusCodes.Status404NotFound,     "Sin Reservas Operativas"),
            ListaEsperaVaciaException           => (StatusCodes.Status404NotFound,     "Lista de Espera Vacía"),
            NotFoundException                   => (StatusCodes.Status404NotFound,     "Recurso No Encontrado"),

            // 409 Conflict — violaciones de reglas de negocio (subclases antes de ConflictException base)
            TransicionEstadoInvalidaException   => (StatusCodes.Status409Conflict,     "Transición de Estado Inválida"),
            MesaConReservaActivaException       => (StatusCodes.Status409Conflict,     "Mesa Con Reserva Activa"),
            MesaSolapadaException               => (StatusCodes.Status409Conflict,     "Mesa No Disponible en ese Horario"),
            SinDisponibilidadException          => (StatusCodes.Status409Conflict,     "Sin Disponibilidad"),
            ReservaDuplicadaClienteException    => (StatusCodes.Status409Conflict,     "Reserva Duplicada"),
            CapacidadInsuficienteException      => (StatusCodes.Status409Conflict,     "Capacidad Insuficiente"),
            ConflictException                   => (StatusCodes.Status409Conflict,     "Conflicto de Negocio"),
            BusinessRuleException               => (StatusCodes.Status409Conflict,     "Regla de Negocio Incumplida"),

            // 500 Internal Server Error — errores inesperados (se loguean con stack trace)
            _ => (StatusCodes.Status500InternalServerError, "Error Interno del Servidor")
        };

        // Solo se loguea como error real si es un fallo inesperado (no errores de negocio)
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception,
                "Excepción no controlada: {ExceptionType} - {Message}",
                exception.GetType().Name,
                exception.Message);
        }
        else
        {
            _logger.LogWarning(
                "Excepción de negocio manejada: [{StatusCode}] {ExceptionType} - {Message}",
                statusCode,
                exception.GetType().Name,
                exception.Message);
        }

        // Construir respuesta ProblemDetails (RFC 7807)
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title  = title,
            Detail = exception.Message,
            Type   = $"https://httpstatuses.com/{statusCode}"
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Retornar true indica que la excepción fue manejada y no debe propagarse
        return true;
    }
}
