namespace Shared.Exceptions;
public class CapacidadInsuficienteException : Exception
{
    public CapacidadInsuficienteException()
        : base("La mesa seleccionada no cuenta con capacidad suficiente para la reserva.") { }
}
