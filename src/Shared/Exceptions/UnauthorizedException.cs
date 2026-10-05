namespace Shared.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Credenciales inválidas.") 
        : base(message)
    {
    }
}
