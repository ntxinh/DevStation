using System.Collections.Concurrent;
using DevStation.Application.Shared.Secrets;

namespace DevStation.Infrastructure.Secrets;

/// <summary>
/// Secret store backed by DEVSTATION_SECRET_&lt;KEY&gt; environment variables
/// (key normalized to [A-Z0-9_]). Runtime SetAsync values live in an in-memory
/// map consulted first; env vars are the boot-time baseline.
/// ponytail: runtime-set secrets are in-memory only; restart loses them —
/// durable store when vault integration lands.
/// </summary>
public sealed class EnvSecretStore : ISecretStore
{
    private const string Prefix = "DEVSTATION_SECRET_";
    private readonly ConcurrentDictionary<string, string> _runtime = new();

    private static string EnvName(string key)
    {
        Span<char> buf = stackalloc char[Prefix.Length + key.Length];
        Prefix.AsSpan().CopyTo(buf);
        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            buf[Prefix.Length + i] = char.IsLetterOrDigit(c)
                ? char.ToUpperInvariant(c)
                : '_';
        }
        return new string(buf);
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _runtime[key] = value;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult(
            _runtime.TryGetValue(key, out var v)
                ? v
                : Environment.GetEnvironmentVariable(EnvName(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _runtime.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
