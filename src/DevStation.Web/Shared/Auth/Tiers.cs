using DevStation.Application.Shared.Security;
using Microsoft.AspNetCore.Authorization;

namespace DevStation.Web.Shared.Auth;

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
