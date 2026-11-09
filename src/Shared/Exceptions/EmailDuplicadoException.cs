namespace Shared.Exceptions;

public class EmailDuplicadoException : ConflictException
{
    public EmailDuplicadoException(string email = "")
        : base(string.IsNullOrWhiteSpace(email) 
            ? "Ya existe un usuario registrado con el correo electrónico provisto." 
            : $"Ya existe un usuario registrado con el correo electrónico provisto: {email.Trim()}.")
    {
    }
}

