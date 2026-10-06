using System.Text.Json;
using DevStation.Application.Modules.Environments;
using DevStation.Application.Modules.Environments.CompareEnvironments;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;

namespace DevStation.Tests.Unit.Environments;

public class CompareTests
{
    private static AppEnvironment Env(string name, params EnvironmentVariable[] vars)
        => new() { Name = name, Variables = vars.ToList() };
    private static Service Svc(string name) => new() { Name = name };

    [Fact]
    public void Services_flag_presence_in_each_environment()
    {
        var diff = EnvironmentCompare.Diff(
            Env("a"), Env("b"),
            [Svc("api"), Svc("worker")],
            [Svc("api"), Svc("db")],
            [], []);

        Assert.Equal(3, diff.Services.Count);
        var api = diff.Services.Single(s => s.Name == "api");
        Assert.True(api.InA);
        Assert.True(api.InB);
        var worker = diff.Services.Single(s => s.Name == "worker");
        Assert.True(worker.InA);
        Assert.False(worker.InB);
        var db = diff.Services.Single(s => s.Name == "db");
        Assert.False(db.InA);
        Assert.True(db.InB);
    }

    [Fact]
    public void Non_secret_variable_differs_when_values_differ()
    {
        var diff = EnvironmentCompare.Diff(
            Env("a", new EnvironmentVariable("LOG_LEVEL", "info", false)),
            Env("b", new EnvironmentVariable("LOG_LEVEL", "debug", false)),
            [], [], [], []);

        var v = diff.Variables.Single(x => x.Key == "LOG_LEVEL");
        Assert.True(v.Differs);
        Assert.False(v.Masked);
    }

    [Fact]
    public void Non_secret_variable_same_value_does_not_differ()
    {
        var diff = EnvironmentCompare.Diff(
            Env("a", new EnvironmentVariable("LOG_LEVEL", "info", false)),
            Env("b", new EnvironmentVariable("LOG_LEVEL", "info", false)),
            [], [], [], []);
        Assert.False(diff.Variables.Single().Differs);
    }

    [Fact]
    public void Variable_present_in_only_one_env_differs()
    {
        var diff = EnvironmentCompare.Diff(
            Env("a", new EnvironmentVariable("ONLY_A", "1", false)),
            Env("b"),
            [], [], [], []);
        var v = diff.Variables.Single();
        Assert.Equal("ONLY_A", v.Key);
        Assert.True(v.Differs);
    }

    [Fact]
    public void Secret_variables_compare_by_presence_only_never_value()
    {
        // same key, both secret, different secret values → no diff reported
        var diff = EnvironmentCompare.Diff(
            Env("a", new EnvironmentVariable("DB_PASS", "hunter2", true)),
            Env("b", new EnvironmentVariable("DB_PASS", "totally-different", true)),
            [], [], [], []);

        var v = diff.Variables.Single();
        Assert.Equal("DB_PASS", v.Key);
        Assert.True(v.Masked);
        Assert.False(v.Differs);
    }

    [Fact]
    public void Secret_variable_missing_in_one_env_differs()
    {
        var diff = EnvironmentCompare.Diff(
            Env("a", new EnvironmentVariable("DB_PASS", "x", true)),
            Env("b"),
            [], [], [], []);
        var v = diff.Variables.Single();
        Assert.True(v.Masked);
        Assert.True(v.Differs);
    }

    [Fact]
    public void Compare_reports_version_commit_deployedAt_per_env()
    {
        var a = Env("a");
        var b = Env("b");
        var depA = new Deployment
        {
            EnvironmentId = a.Id,
            Version = "1.0",
            Commit = "aaa",
            Status = DeploymentStatus.Succeeded,
            FinishedAt = DateTimeOffset.UtcNow,
        };

        var diff = EnvironmentCompare.Diff(a, b, [], [], [depA], []);

        Assert.Equal("1.0", diff.Version.A);
        Assert.Null(diff.Version.B);
        Assert.Equal("aaa", diff.Commit.A);
        Assert.Null(diff.Commit.B);
        Assert.NotNull(diff.DeployedAt.A);
        Assert.Null(diff.DeployedAt.B);
    }

    [Fact]
    public void Latest_succeeded_deployment_wins_over_older_or_failed()
    {
        var a = Env("a");
        var older = new Deployment
        {
            EnvironmentId = a.Id,
            Version = "0.9",
            Commit = "old",
            Status = DeploymentStatus.Succeeded,
            FinishedAt = DateTimeOffset.UtcNow.AddDays(-2),
        };
        var failed = new Deployment
        {
            EnvironmentId = a.Id,
            Version = "9.9",
            Commit = "bad",
            Status = DeploymentStatus.Failed,
            FinishedAt = DateTimeOffset.UtcNow,
        };
        var diff = EnvironmentCompare.Diff(a, Env("b"), [], [], [older, failed], []);
        Assert.Equal("0.9", diff.Version.A);
    }

    // --- masking (unit: secret value never reaches a DTO) ---

    [Fact]
    public void EnvironmentDto_masks_secret_values()
    {
        var env = Env("prod", new EnvironmentVariable("DB_PASS", "hunter2", true), new EnvironmentVariable("COLOR", "blue", false));
        var dto = EnvironmentDto.Of(env, "unknown");

        var secret = dto.Variables.Single(v => v.Key == "DB_PASS");
        Assert.True(secret.Secret);
        Assert.Null(secret.Value);
        var plain = dto.Variables.Single(v => v.Key == "COLOR");
        Assert.Equal("blue", plain.Value);
    }

    [Fact]
    public void Serialized_dto_never_contains_secret_value()
    {
        var env = Env("prod", new EnvironmentVariable("DB_PASS", "s3cr3t-token-xyz", true));
        var json = JsonSerializer.Serialize(EnvironmentDto.Of(env, "unknown"));
        Assert.DoesNotContain("s3cr3t-token-xyz", json);
    }
}
