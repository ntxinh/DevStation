namespace DevStation.Application.Shared.Providers;

public sealed record GitStatus(string Branch, string? CommitSha, string? CommitMessage,
    bool Dirty, int Ahead, int Behind);

public sealed record CommitInfo(string Sha, string Message, string Author, DateTimeOffset When);

public interface IGitProvider
{
    /// <summary>Throws AppException GIT_PATH_FORBIDDEN / GIT_NOT_REPO on unusable paths.</summary>
    GitStatus GetStatus(string path);
    IReadOnlyList<CommitInfo> GetRecentCommits(string path, int n);
}
