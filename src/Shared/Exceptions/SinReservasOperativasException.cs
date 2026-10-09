namespace Shared.Exceptions;

public class SinReservasOperativasException : NotFoundException
{
    public SinReservasOperativasException()
        : base("No hay reservas operativas registradas para la fecha especificada.")
    {
    }
}
