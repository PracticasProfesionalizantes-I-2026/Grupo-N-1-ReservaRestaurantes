namespace Shared.Exceptions;

public class SinDisponibilidadException : ConflictException
{
    public SinDisponibilidadException(string message = "No hay disponibilidad de mesas para el horario y cantidad de personas solicitadas. Puede ingresar a la lista de espera.") 
        : base(message) { }
}
