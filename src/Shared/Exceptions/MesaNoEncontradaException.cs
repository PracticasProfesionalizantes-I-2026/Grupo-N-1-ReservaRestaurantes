using Shared.Exceptions;

namespace Shared.Exceptions;

public class MesaNoEncontradaException : NotFoundException
{
    public MesaNoEncontradaException(Guid id)
        : base($"La mesa especificada con ID '{id}' no existe.")
    {
    }
}
