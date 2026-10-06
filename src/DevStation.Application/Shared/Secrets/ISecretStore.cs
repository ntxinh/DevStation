namespace DevStation.Application.Shared.Secrets;

/// <summary>Key/value secret vault seam. Values never flow through DTOs or the DB —
/// entities carry a SecretRef holding only the key.</summary>
public interface ISecretStore
{
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
