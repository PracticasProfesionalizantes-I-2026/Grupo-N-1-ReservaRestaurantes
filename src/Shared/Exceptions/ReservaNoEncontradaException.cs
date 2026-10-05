using Shared.Exceptions;

namespace Shared.Exceptions;

public class ReservaNoEncontradaException : NotFoundException
{
    public ReservaNoEncontradaException(Guid reservaId)
        : base($"La reserva solicitada con ID '{reservaId}' no fue encontrada.")
    {
    }
}
