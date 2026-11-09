namespace Shared.Exceptions;

/// <summary>
/// Excepción lanzada cuando un usuario autenticado intenta acceder o modificar
/// un recurso que no le pertenece o para el cual no posee permisos suficientes (HTTP 403 Forbidden).
/// </summary>
public class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(string message = "No posee permisos para realizar esta operación sobre el recurso solicitado.")
        : base(message)
    {
    }

    public AccesoDenegadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

