namespace Shared.Exceptions;

public class MesaNoCompatibleException : ConflictException
{
    public MesaNoCompatibleException(string message = "La mesa no se encuentra disponible o no cuenta con capacidad compatible.")
        : base(message)
    {
    }
}

