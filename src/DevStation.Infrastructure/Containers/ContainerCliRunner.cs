using System.Diagnostics;
using DevStation.Application.Shared.Results;

namespace DevStation.Infrastructure.Containers;

/// <summary>Executes a container-runtime binary via ProcessStartInfo.ArgumentList —
/// no shell, no string interpolation of args. Non-zero exit → CONTAINER_CLI_ERROR 502
/// with stderr truncated to 500 chars.</summary>
public class ContainerCliRunner
{
    public virtual async Task<string> RunAsync(string exe, IReadOnlyList<string> args,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = new Process { StartInfo = psi };
        try
        {
            if (!p.Start())
                throw CliError(exe, "failed to start process");
        }
        catch (Exception e) when (e is not AppException)
        {
            throw CliError(exe, e.Message);
        }
        await using var _ = ct.Register(() => { try { p.Kill(); } catch { /* exited */ } })
            .ConfigureAwait(false);
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        var err = await stderr;
        if (p.ExitCode != 0)
            throw CliError(exe, err, $"exit code {p.ExitCode}");
        return await stdout;
    }

    private static AppException CliError(string exe, string stderr, string? fallback = null)
    {
        var msg = stderr.Trim();
        if (msg.Length == 0) msg = fallback ?? $"{exe} failed";
        if (msg.Length > 500) msg = msg[..500];
        return new AppException("CONTAINER_CLI_ERROR", 502, $"{exe}: {msg}");
    }
}
