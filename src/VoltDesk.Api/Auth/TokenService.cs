using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VoltDesk.Core.Employees;

namespace VoltDesk.Api.Auth;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(Employee employee)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.Add(jwt.Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, employee.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, employee.FullName),
                new Claim(JwtRegisteredClaimNames.Email, employee.Email),
                new Claim(Claims.Role, employee.Role.ToString()),
            ]),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
