using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BusinessLogic.Auth.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using EntityCliente = DataAccess.Entities.Cliente;

namespace BusinessLogic.Auth.Implementations;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public (string Token, DateTime Expiration) GenerateToken(EntityCliente cliente)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "SuperSecretKey_GrupoN1_ReservaRestaurantes_2026!";
        var issuer = _configuration["Jwt:Issuer"] ?? "ReservaRestaurantesApi";
        var audience = _configuration["Jwt:Audience"] ?? "ReservaRestaurantesUsers";
        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, cliente.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, cliente.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, cliente.Email),
            new Claim(ClaimTypes.Email, cliente.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, cliente.Nombre),
            new Claim(JwtRegisteredClaimNames.FamilyName, cliente.Apellido),
            new Claim(ClaimTypes.Role, string.IsNullOrWhiteSpace(cliente.Rol) ? "Cliente" : cliente.Rol)
        };

        var expiration = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiration,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiration);
    }
}
