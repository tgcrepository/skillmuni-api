using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkillMuni.Common.Results;
using SkillMuni.Common.Security;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Microsoft.Extensions.DependencyInjection;

public static class AuthenticationExtensions
{
    /// <summary>
    /// JWT validation from the HttpOnly access cookie (or a bearer header), with SkillMuni-shaped
    /// 401/403 responses and the learner / institute-admin / tenant-admin policies.
    /// </summary>
    public static IServiceCollection AddSkillMuniAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.Section)
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningPublicKeyPem), "Auth:SigningPublicKeyPem is required.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) => Configure(jwt, auth.Value));

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Learner, p => p.RequireAuthenticatedUser().RequireRole(Policies.Learner))
            .AddPolicy(Policies.InstituteAdmin, p => p.RequireAuthenticatedUser().RequireRole(Policies.InstituteAdmin))
            .AddPolicy(Policies.TenantAdmin, p => p.RequireAuthenticatedUser().RequireRole(Policies.TenantAdmin));

        return services;
    }

    private static void Configure(JwtBearerOptions jwt, AuthOptions auth)
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = auth.Issuer,
            ValidateAudience = true,
            ValidAudience = auth.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = RsaKeys.Load(auth.SigningPublicKeyPem),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = SkillMuniClaims.Name,
            RoleClaimType = SkillMuniClaims.Role,
        };
        jwt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(auth.AccessCookieName, out var token) && !string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ApiProblems.WriteAsync(context.HttpContext, Error.Unauthorized("no-session", "No session"));
            },
            OnForbidden = context =>
                ApiProblems.WriteAsync(context.HttpContext, Error.Forbidden("forbidden", "You don't have access to this.")),
        };
    }
}
