namespace Shared.Exceptions;

public class ReservaDuplicadaClienteException : ConflictException
{
    public ReservaDuplicadaClienteException(string message = "Ya posee una reserva registrada o activa para esa franja horaria.") 
        : base(message) { }
}
