namespace Shared.Exceptions;

/// <summary>
/// Excepción lanzada cuando un usuario intenta iniciar sesión o realizar operaciones
/// con una cuenta que se encuentra deshabilitada / inactiva (HTTP 403 Forbidden).
/// </summary>
public class CuentaInactivaException : Exception
{
    public CuentaInactivaException(string message = "La cuenta de usuario se encuentra deshabilitada.")
        : base(message)
    {
    }
}

