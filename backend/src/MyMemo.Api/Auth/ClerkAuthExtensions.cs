using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MyMemo.Api.Auth;

public static class ClerkAuthExtensions
{
    public static IServiceCollection AddClerkAuth(this IServiceCollection services, IConfiguration config)
    {
        var clerkDomain = config["Clerk:Domain"]
            ?? throw new InvalidOperationException("Clerk:Domain not configured");

        if (string.IsNullOrWhiteSpace(clerkDomain))
            throw new InvalidOperationException("Clerk:Domain is empty — check appsettings.Development.json");

        var authority = $"https://{clerkDomain}";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    NameClaimType = "sub"
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("ClerkAuth");
                        logger.LogError(context.Exception, "JWT authentication failed");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("ClerkAuth");
                        logger.LogInformation("JWT validated for {Sub}",
                            context.Principal?.FindFirst("sub")?.Value);
                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("ClerkAuth");
                        var hasToken = !string.IsNullOrEmpty(
                            context.Request.Headers.Authorization.FirstOrDefault());
                        logger.LogInformation("Auth header present: {HasToken}, Authority: {Authority}",
                            hasToken, authority);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
