using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace VoltDesk.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "voltdesk";

    [Required]
    public string Audience { get; set; } = "voltdesk-desktop";

    /// <summary>HMAC key, at least 32 bytes. Set through configuration or the Jwt__SigningKey variable.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
