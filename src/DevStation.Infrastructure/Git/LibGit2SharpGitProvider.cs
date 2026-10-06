using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;

namespace DevStation.Infrastructure.Git;

public sealed class LibGit2SharpGitProvider : IGitProvider
{
    private readonly string[] _roots;

    public LibGit2SharpGitProvider(IConfiguration config)
        : this(ResolveRoots(config)) { }

    // test seam: pass allowed roots directly
    public LibGit2SharpGitProvider(string[] allowedRoots) => _roots = allowedRoots;

    private static string[] ResolveRoots(IConfiguration config)
    {
        var roots = config.GetSection("DevStation:AllowedRepoRoots")
            .GetChildren().Select(c => c.Value).OfType<string>().ToArray();
        return roots.Length > 0
            ? roots
            : [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)];
    }

    private string Guarded(string path)
    {
        var full = Path.GetFullPath(path);
        var ok = _roots.Any(r =>
        {
            var root = Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar);
            return full == root || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        });
        if (!ok)
            throw new AppException("GIT_PATH_FORBIDDEN", 403,
                $"Path '{path}' is outside DevStation:AllowedRepoRoots");
        if (!Repository.IsValid(full))
            throw new AppException("GIT_NOT_REPO", 400, $"'{path}' is not a git repository");
        return full;
    }

    public GitStatus GetStatus(string path)
    {
        using var repo = new Repository(Guarded(path));
        var head = repo.Head;
        var t = head.TrackingDetails;
        return new GitStatus(head.FriendlyName, head.Tip?.Sha, head.Tip?.MessageShort,
            repo.RetrieveStatus().IsDirty, t?.AheadBy ?? 0, t?.BehindBy ?? 0);
    }

    public IReadOnlyList<CommitInfo> GetRecentCommits(string path, int n)
    {
        using var repo = new Repository(Guarded(path));
        return repo.Commits.Take(n)
            .Select(c => new CommitInfo(c.Sha, c.MessageShort, c.Author.Name, c.Author.When))
            .ToList();
    }
}
