using System.Collections.Concurrent;
using BusinessLogic.Auth.Interfaces;

namespace BusinessLogic.Auth.Implementations;

/// <summary>
/// Implementación concurrente en memoria de la lista de revocación de tokens.
/// Mantiene registros de tokens individuales revocados y marcas temporales de revocación por usuario.
/// </summary>
public class TokenRevocationService : ITokenRevocationService
{
    private readonly ConcurrentDictionary<string, DateTime> _revokedTokens = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _revokedUsers = new();

    public void RevokeToken(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _revokedTokens[token.Trim()] = DateTime.UtcNow;
        }
    }

    public void RevokeUserTokens(Guid userId)
    {
        _revokedUsers[userId] = DateTime.UtcNow;
    }

    public bool IsTokenRevoked(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _revokedTokens.ContainsKey(token.Trim());
    }

    public bool IsUserRevoked(Guid userId, DateTime? tokenIssuedAt)
    {
        if (_revokedUsers.TryGetValue(userId, out var revokedAt))
        {
            if (!tokenIssuedAt.HasValue)
            {
                return true;
            }

            // Si el token fue emitido antes o en el mismo instante de la revocación, se invalida
            return tokenIssuedAt.Value <= revokedAt;
        }

        return false;
    }

    public bool IsRevoked(string? token, Guid? userId, DateTime? tokenIssuedAt)
    {
        if (!string.IsNullOrWhiteSpace(token) && IsTokenRevoked(token))
        {
            return true;
        }

        if (userId.HasValue && IsUserRevoked(userId.Value, tokenIssuedAt))
        {
            return true;
        }

        return false;
    }
}

