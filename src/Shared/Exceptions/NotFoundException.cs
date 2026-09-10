namespace Shared.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"La entidad '{entityName}' con identificador '{key}' no fue encontrada.")
    {
    }
}
