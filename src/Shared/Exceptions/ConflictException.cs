namespace Shared.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string entityName, string field, object value)
        : base($"Ya existe un registro de '{entityName}' con {field} '{value}'.")
    {
    }
}
