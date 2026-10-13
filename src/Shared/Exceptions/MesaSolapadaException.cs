namespace Shared.Exceptions;

public class MesaSolapadaException : Exception
{
    public MesaSolapadaException()
        : base("La mesa seleccionada ya se encuentra comprometida en ese horario.") { }
}
