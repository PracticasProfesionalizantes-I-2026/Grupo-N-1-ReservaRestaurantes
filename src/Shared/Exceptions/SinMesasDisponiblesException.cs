namespace Shared.Exceptions;

public class SinMesasDisponiblesException : NotFoundException
{
    public SinMesasDisponiblesException()
        : base("No se encontraron mesas que coincidan con los filtros aplicados.")
    {
    }
}
