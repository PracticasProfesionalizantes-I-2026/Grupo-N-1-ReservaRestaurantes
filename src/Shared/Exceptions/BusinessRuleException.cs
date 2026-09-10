namespace Shared.Exceptions;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string ruleName, string message)
        : base($"[Regla de Negocio: {ruleName}] {message}")
    {
    }
}
