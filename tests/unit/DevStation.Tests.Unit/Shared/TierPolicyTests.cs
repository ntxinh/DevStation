using DevStation.Application.Shared.Security;

namespace DevStation.Tests.Unit.Shared;

public class TierPolicyTests
{
    [Theory]
    [InlineData("admin","read",true)][InlineData("read","admin",false)]
    [InlineData("deploy","execute",true)][InlineData("execute","deploy",false)]
    public void Rank_comparison(string user,string required,bool expected)
        => Assert.Equal(expected, Tiers.Rank(user) >= Tiers.Rank(required));
}
