using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DevStation.Web.Shared.Auth;

public sealed class DevTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = config["DevStation:Token"];
        if (string.IsNullOrEmpty(expected))
            return Task.FromResult(AuthenticateResult.Fail("DevStation:Token not configured"));

        var token = Request.Headers.Authorization is [{ } h] && h.StartsWith("Bearer ", StringComparison.Ordinal)
            ? h["Bearer ".Length..]
            : Request.Query["access_token"].ToString(); // SignalR

        if (!string.Equals(token, expected, StringComparison.Ordinal) &&
            !config.GetSection("DevStation:TokenTiers")
                .GetChildren().Any(c => c.Key == token))
            return Task.FromResult(AuthenticateResult.Fail("Invalid or missing dev token"));

        // main token is admin; extra tokens get their configured tier
        // (DevStation:TokenTiers:<token> = read|execute|deploy|admin)
        var tier = token == expected ? "admin"
            : config[$"DevStation:TokenTiers:{token}"] ?? "admin";
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "dev"), new Claim("tier", tier)],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
