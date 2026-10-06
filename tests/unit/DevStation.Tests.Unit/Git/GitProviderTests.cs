using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.Git;
using LibGit2Sharp;

namespace DevStation.Tests.Unit.Git;

public class GitProviderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "ds-git-" + Guid.NewGuid().ToString("N"));

    private readonly LibGit2SharpGitProvider _sut;

    public GitProviderTests()
    {
        Directory.CreateDirectory(_root);
        _sut = new LibGit2SharpGitProvider([_root]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup best-effort */ }
    }

    private string InitRepoWithCommit()
    {
        var dir = Path.Combine(_root, "repo");
        Repository.Init(dir);
        using var repo = new Repository(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "hi");
        Commands.Stage(repo, "a.txt");
        var sig = new Signature("t", "t@t", DateTimeOffset.UtcNow);
        return repo.Commit("initial", sig, sig).Sha;
    }

    [Fact]
    public void GetStatus_returns_branch_sha_and_clean()
    {
        var sha = InitRepoWithCommit();
        var s = _sut.GetStatus(Path.Combine(_root, "repo"));
        Assert.Contains(s.Branch, new[] { "master", "main" });
        Assert.Equal(sha, s.CommitSha);
        Assert.Equal("initial", s.CommitMessage);
        Assert.False(s.Dirty);
    }

    [Fact]
    public void GetStatus_reports_dirty()
    {
        var dir = Path.Combine(_root, "repo");
        InitRepoWithCommit();
        File.WriteAllText(Path.Combine(dir, "a.txt"), "changed");
        Assert.True(_sut.GetStatus(dir).Dirty);
    }

    [Fact]
    public void GetRecentCommits_returns_newest_first()
    {
        InitRepoWithCommit();
        var commits = _sut.GetRecentCommits(Path.Combine(_root, "repo"), 10);
        var c = Assert.Single(commits);
        Assert.Equal("initial", c.Message);
        Assert.Equal("t", c.Author);
    }

    [Fact]
    public void Non_repo_path_throws_GIT_NOT_REPO()
    {
        var dir = Path.Combine(_root, "plain");
        Directory.CreateDirectory(dir);
        var ex = Assert.Throws<AppException>(() => _sut.GetStatus(dir));
        Assert.Equal("GIT_NOT_REPO", ex.Code);
        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public void Path_outside_allowed_roots_throws_GIT_PATH_FORBIDDEN()
    {
        var ex = Assert.Throws<AppException>(() => _sut.GetStatus("/proc/ds-forbidden"));
        Assert.Equal("GIT_PATH_FORBIDDEN", ex.Code);
        Assert.Equal(403, ex.Status);
    }
}
