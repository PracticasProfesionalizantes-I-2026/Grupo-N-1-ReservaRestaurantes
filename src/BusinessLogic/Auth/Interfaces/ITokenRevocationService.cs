namespace BusinessLogic.Auth.Interfaces;

/// <summary>
/// Servicio de lista de revocación de tokens JWT y sesiones de usuario.
/// Permite invalidar tokens individuales al cerrar sesión y todos los tokens de un usuario
/// al cambiar de rol o al dar de baja la cuenta (RN-03 de CU-19 / SEC-07).
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>
    /// Revoca un token JWT específico (ej. cierre de sesión).
    /// </summary>
    void RevokeToken(string token);

    /// <summary>
    /// Revoca todos los tokens emitidos para un usuario antes de este momento
    /// (ej. cambio de rol o baja de usuario).
    /// </summary>
    void RevokeUserTokens(Guid userId);

    /// <summary>
    /// Determina si un token individual ha sido revocado.
    /// </summary>
    bool IsTokenRevoked(string token);

    /// <summary>
    /// Determina si los tokens de un usuario emitidos en o antes de la fecha indicada han sido revocados.
    /// </summary>
    bool IsUserRevoked(Guid userId, DateTime? tokenIssuedAt);

    /// <summary>
    /// Comprueba de forma integral si el token o el usuario asociado han sido revocados.
    /// </summary>
    bool IsRevoked(string? token, Guid? userId, DateTime? tokenIssuedAt);
}

