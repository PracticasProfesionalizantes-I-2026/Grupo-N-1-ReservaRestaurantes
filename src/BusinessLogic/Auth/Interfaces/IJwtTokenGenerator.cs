using DataAccess.Entities;

namespace BusinessLogic.Auth.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime Expiration) GenerateToken(DataAccess.Entities.Cliente cliente);
}
