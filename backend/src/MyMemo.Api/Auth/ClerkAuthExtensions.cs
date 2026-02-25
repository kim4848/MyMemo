using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MyMemo.Api.Auth;

public static class ClerkAuthExtensions
{
    public static IServiceCollection AddClerkAuth(this IServiceCollection services, IConfiguration config)
    {
        var clerkDomain = config["Clerk:Domain"]
            ?? throw new InvalidOperationException("Clerk:Domain not configured");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{clerkDomain}";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{clerkDomain}",
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    NameClaimType = "sub"
                };
            });

        services.AddAuthorization();

        return services;
    }
}
