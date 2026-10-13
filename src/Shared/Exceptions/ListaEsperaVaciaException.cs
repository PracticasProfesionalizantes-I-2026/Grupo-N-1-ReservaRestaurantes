using Shared.Exceptions;

namespace Shared.Exceptions;

public class ListaEsperaVaciaException : NotFoundException
{
    public ListaEsperaVaciaException()
        : base("Actualmente no hay ninguna reserva en lista de espera.")
    {
    }
}
