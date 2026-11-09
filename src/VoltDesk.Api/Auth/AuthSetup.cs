using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VoltDesk.Core.Employees;
using VoltDesk.Infrastructure;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Auth;

public static class AuthSetup
{
    public const string LoginRateLimit = "login";

    public static IServiceCollection AddVoltDeskAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpContextActor>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateKey(),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = Claims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectDisabledOrChangedAccounts };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.ManageProjects, policy => policy.RequireRole(Policies.ManagerRoles))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(EmployeeRole.Admin)));

        var loginsPerMinute = configuration.GetValue("Auth:LoginAttemptsPerMinute", 10);
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginsPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>
    /// Tokens live for hours, so check the account on every request: a deactivated employee or a
    /// changed role takes effect immediately instead of when the token expires.
    /// </summary>
    private static async Task RejectDisabledOrChangedAccounts(TokenValidatedContext context)
    {
        var employeeId = context.Principal?.EmployeeId();
        var role = context.Principal?.FindFirst(Claims.Role)?.Value;
        var db = context.HttpContext.RequestServices.GetRequiredService<VoltDeskDbContext>();

        var current = employeeId is null
            ? null
            : await db.Employees.AsNoTracking()
                .Where(e => e.Id == employeeId)
                .Select(e => new { e.IsActive, e.Role })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (current is null || !current.IsActive || current.Role.ToString() != role)
        {
            context.Fail("The account is disabled or its role has changed. Sign in again.");
        }
    }
}
