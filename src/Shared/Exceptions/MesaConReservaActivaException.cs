using Shared.Exceptions;

namespace Shared.Exceptions;

public class MesaConReservaActivaException : ConflictException
{
    public MesaConReservaActivaException()
        : base("No se puede marcar la mesa como Libre mientras tenga una reserva en curso activa.")
    {
    }
}
