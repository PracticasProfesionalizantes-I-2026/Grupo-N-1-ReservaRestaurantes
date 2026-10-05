namespace Shared.Exceptions;

public class HorarioNoHabilitadoException : BusinessRuleException
{
    public HorarioNoHabilitadoException(string message = "El horario seleccionado no se encuentra habilitado para reservas.") 
        : base("RN-03", message) { }
}
