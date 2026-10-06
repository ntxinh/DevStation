using DevStation.Application.Modules.Deployments.StartDeployment;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Tests.Unit.Deployments;

public class DeployPolicyTests : IDisposable
{
    private readonly DevStationDbContext _db = new(
        new DbContextOptionsBuilder<DevStationDbContext>()
            .UseInMemoryDatabase("deploy-policy").Options);
    private readonly StartDeploymentHandler _handler;

    public DeployPolicyTests() => _handler = new StartDeploymentHandler(_db, null!, null!);

    public void Dispose() => _db.Dispose();

    private async Task<Guid> AddEnv(string type)
    {
        var e = new AppEnvironment { Name = $"env-{type}", Type = type };
        _db.Environments.Add(e);
        await _db.SaveChangesAsync();
        return e.Id;
    }

    [Theory]
    [InlineData("dev", "Execute")]
    [InlineData("qa", "Execute")]
    [InlineData("staging", "Execute")]
    [InlineData("production", "Deploy")]
    public async Task Required_policy_by_env_type(string type, string expected)
    {
        var id = await AddEnv(type);
        Assert.Equal(expected, await _handler.RequiredPolicyAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_env_throws_404()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => _handler.RequiredPolicyAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(404, ex.Status);
    }
}
