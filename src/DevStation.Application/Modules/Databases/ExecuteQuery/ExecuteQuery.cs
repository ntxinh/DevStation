using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Application.Shared.Security;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.ExecuteQuery;

public sealed record ExecuteQueryRequest
{
    public string Sql { get; init; } = "";
    public int? MaxRows { get; init; }
    public int? TimeoutSeconds { get; init; }
}

public sealed record ExecuteQueryResponse(QueryResult Result, SqlTier Tier,
    bool MultiStatement);

/// <summary>Guardrails run before any provider call, strictest first:
/// ReadOnly conn blocks Write+ (SQL_READONLY); a connection linked to a
/// production-type environment blocks Write+ unconditionally
/// (SQL_PROD_WRITE_BLOCKED — confirm cannot bypass); Dangerous tier requires
/// confirm=true (CONFIRMATION_REQUIRED). Every attempted execution writes a
/// QueryHistory row even when the request dies mid-flight.</summary>
public sealed class ExecuteQueryHandler(IDevStationDb db,
    IEnumerable<IDatabaseProvider> providers, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task<ExecuteQueryResponse> Handle(Guid id,
        ExecuteQueryRequest r, bool confirm, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var conn = await db.DatabaseConnections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Connection not found");
        var sql = r.Sql ?? "";
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > 1_000_000)
            throw new AppException("VALIDATION_ERROR", 400,
                "sql must be 1-1000000 characters");

        var verdict = SqlGuard.Classify(sql);
        var tier = verdict.Tier;

        if (conn.ReadOnly && tier > SqlTier.Read)
            throw new AppException("SQL_READONLY", 403,
                $"connection is read-only ({verdict.Reason})");

        var envType = conn.EnvironmentId is { } envId
            ? await db.Environments.Where(e => e.Id == envId)
                .Select(e => e.Type).FirstOrDefaultAsync(ct)
            : null;
        if (envType == "production" && tier > SqlTier.Read)
            throw new AppException("SQL_PROD_WRITE_BLOCKED", 403,
                "writes are never allowed on production-linked connections");

        if (tier == SqlTier.Dangerous && !confirm)
            throw new AppException("CONFIRMATION_REQUIRED", 403,
                $"dangerous statement requires confirm=true ({verdict.Reason})");

        var maxRows = Math.Clamp(r.MaxRows ?? conn.MaxRows, 1, 10_000);
        var timeout = Math.Clamp(r.TimeoutSeconds ?? conn.TimeoutSeconds, 1, 300);
        var pw = conn.PasswordRef is null ? null
            : await secrets.GetAsync(conn.PasswordRef.Key, ct);

        QueryResult? result = null;
        string? errorCode = null;
        string? errorMessage = null;
        try
        {
            result = await providers.For(conn.Kind)
                .ExecuteQueryAsync(conn, sql, pw, maxRows, timeout, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            errorCode = "QUERY_TIMEOUT";
            errorMessage = $"command exceeded {timeout}s timeout";
        }
        catch (OperationCanceledException)
        {
            errorCode = "QUERY_CANCELLED";
            errorMessage = "query cancelled";
        }
        catch (Exception ex) when (ex is TimeoutException
                                   || ex.InnerException is TimeoutException)
        {
            // CommandTimeout expiry surfaces as TimeoutException (often wrapped)
            errorCode = "QUERY_TIMEOUT";
            errorMessage = $"command exceeded {timeout}s timeout";
        }
        catch (AppException) { throw; }
        catch (Exception ex)
        {
            errorCode = "QUERY_FAILED";
            errorMessage = ex.Message;
        }

        // history row for every attempted execution — CancellationToken.None so a
        // client abort can't skip the audit trail
        db.QueryHistory.Add(new QueryHistoryEntry
        {
            ConnectionId = conn.Id,
            Sql = sql.Length > 4000 ? sql[..4000] : sql,
            RowCount = result?.RowCount,
            DurationMs = result?.DurationMs,
            Success = result is not null,
            Error = errorMessage is null ? null
                : $"{errorCode}: {(errorMessage.Length > 400 ? errorMessage[..400] : errorMessage)}",
        });
        await db.SaveChangesAsync(CancellationToken.None);
        await audit.Record("database.query", "database_connection", conn.Id,
            user.Identity?.Name, result is null ? "error" : "ok",
            new { tier = tier.ToString(), verdict.MultiStatement,
                  rows = result?.RowCount, ms = result?.DurationMs, errorCode },
            CancellationToken.None);

        if (errorCode is not null)
            throw new AppException(errorCode, 400, errorMessage!);
        return new ExecuteQueryResponse(result!, tier, verdict.MultiStatement);
    }
}
