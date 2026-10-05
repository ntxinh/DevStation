using Microsoft.AspNetCore.Authorization;

namespace DevStation.Web.Shared.Auth;

public static class Tiers
{
    public static int Rank(string t) => t switch
    {
        "read" => 1,
        "execute" => 2,
        "deploy" => 3,
        "admin" => 4,
        _ => 0,
    };
}

public sealed class TierRequirement(string tier) : IAuthorizationRequirement
{
    public string Tier { get; } = tier;
}

public sealed class TierHandler : AuthorizationHandler<TierRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TierRequirement requirement)
    {
        var tier = context.User.FindFirst("tier")?.Value;
        if (tier is not null && Tiers.Rank(tier) >= Tiers.Rank(requirement.Tier))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
