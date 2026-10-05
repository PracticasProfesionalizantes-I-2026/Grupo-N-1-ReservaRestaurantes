namespace Shared.Exceptions;

public class TransicionEstadoInvalidaException : Exception
{
    public TransicionEstadoInvalidaException(string message = "No es posible cambiar el estado de la reserva en su condición actual.")
        : base(message)
    {
    }
}
