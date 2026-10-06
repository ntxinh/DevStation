using System.Text.Json;
using System.Text.RegularExpressions;
using DevStation.Application.Shared.Providers;

namespace DevStation.Infrastructure.Containers;

/// <summary>Parses `--format json` output. Podman emits a JSON array; docker emits
/// JSONL (one object per line) — both accepted.</summary>
public static class ContainerOutputParser
{
    private static readonly Regex Sensitive =
        new(@"(?i)(password|secret|token|key|credential)", RegexOptions.Compiled);

    public static bool IsSensitiveEnvName(string name) => Sensitive.IsMatch(name);

    public static IReadOnlyList<ContainerInfo> ParseList(string stdout)
    {
        var list = new List<ContainerInfo>();
        foreach (var el in Elements(stdout))
            list.Add(ParseInfo(el));
        return list;
    }

    private static ContainerInfo ParseInfo(JsonElement el)
    {
        var id = GetString(el, "Id", "ID");
        // podman: Names is an array; docker: Names is a comma-joined string
        var name = el.TryGetProperty("Names", out var names) &&
                   names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Select(x => x.GetString() ?? "")
                  .FirstOrDefault(x => x.Length > 0) ?? ""
            : GetString(el, "Names", "Name").Split(',')[0].TrimStart('/');

        var ports = new List<string>();
        if (el.TryGetProperty("Ports", out var p))
        {
            if (p.ValueKind == JsonValueKind.Array) // podman: port objects
                foreach (var po in p.EnumerateArray())
                {
                    var hostPort = GetString(po, "host_port", "HostPort");
                    var contPort = GetString(po, "container_port", "ContainerPort");
                    var proto = GetString(po, "protocol", "Protocol");
                    var hostIp = GetString(po, "host_ip", "HostIP");
                    var desc = hostPort.Length > 0
                        ? $"{hostIp}{(hostIp.Length > 0 ? ":" : "")}{hostPort}->{contPort}"
                        : contPort;
                    if (proto.Length > 0) desc += $"/{proto}";
                    if (desc.Length > 0) ports.Add(desc);
                }
            else if (p.ValueKind == JsonValueKind.String) // docker: "a:b->c/d, ..."
                foreach (var s in p.GetString()!.Split(',',
                             StringSplitOptions.RemoveEmptyEntries))
                    ports.Add(s.Trim());
        }

        return new ContainerInfo(id, name, GetString(el, "Image"),
            GetString(el, "State"), GetString(el, "Status"), ports);
    }

    public static ContainerInspect ParseInspect(string stdout)
    {
        var el = Elements(stdout).FirstOrDefault();
        if (el.ValueKind != JsonValueKind.Object)
            throw new JsonException("no inspect object in output");

        var id = GetString(el, "Id", "ID");
        var name = GetString(el, "Name", "Names").TrimStart('/');
        var cfg = GetObject(el, "Config");
        // Config.Image is the friendly tag; top-level Image is a digest under docker
        var image = cfg.ValueKind == JsonValueKind.Object
            ? GetString(cfg, "Image") : "";
        if (image.Length == 0) image = GetString(el, "Image", "ImageName");

        var state = "";
        int? exit = null;
        DateTimeOffset? started = null;
        if (TryGetObject(el, "State", out var st))
        {
            state = GetString(st, "Status", "State");
            exit = TryGetInt(st, "ExitCode", "exit_code");
            started = TryGetDate(st, "StartedAt", "started_at");
        }
        if (state.Length == 0) state = GetString(el, "State", "Status");

        var env = new List<string>();
        if (cfg.ValueKind == JsonValueKind.Object &&
            cfg.TryGetProperty("Env", out var envArr) &&
            envArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in envArr.EnumerateArray())
            {
                var s = v.GetString() ?? "";
                var eq = s.IndexOf('=');
                if (eq > 0 && IsSensitiveEnvName(s[..eq])) continue;
                env.Add(s);
            }
        }

        var mounts = new List<ContainerMount>();
        if (el.TryGetProperty("Mounts", out var mArr) && mArr.ValueKind == JsonValueKind.Array)
            foreach (var m in mArr.EnumerateArray())
                mounts.Add(new ContainerMount(GetString(m, "Type"),
                    GetString(m, "Source", "source"),
                    GetString(m, "Destination", "destination"),
                    TryGetBool(m, "RW", "rw")));

        var nets = new List<ContainerNetwork>();
        if (TryGetObject(el, "NetworkSettings", out var ns) &&
            ns.TryGetProperty("Networks", out var netsObj) &&
            netsObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var n in netsObj.EnumerateObject())
                nets.Add(new ContainerNetwork(n.Name,
                    n.Value.ValueKind == JsonValueKind.Object
                        ? NullIfEmpty(GetString(n.Value, "IPAddress"))
                        : null));
        }
        // podman host: network names sit directly under NetworkSettings
        if (nets.Count == 0 && TryGetObject(el, "NetworkSettings", out var ns2) &&
            ns2.ValueKind == JsonValueKind.Object)
            foreach (var n in ns2.EnumerateObject())
                nets.Add(new ContainerNetwork(n.Name, null));

        return new ContainerInspect(id, name, image, state, exit, started,
            TryGetDate(el, "Created"), env, mounts, nets);
    }

    public static IReadOnlyList<ContainerStats> ParseStats(string stdout)
    {
        var list = new List<ContainerStats>();
        foreach (var el in Elements(stdout))
        {
            var net = GetString(el, "net_io", "NetIO");
            if (net.Length == 0) net = Combine(el, "net_input", "net_output");
            var pids = GetString(el, "pids", "PIDs");
            list.Add(new ContainerStats(
                GetString(el, "id", "ID", "ContainerID"),
                GetString(el, "name", "Name"),
                NullIfEmpty(GetString(el, "cpu_percent", "CPUPerc")),
                NullIfEmpty(GetString(el, "mem_usage", "MemUsage")),
                NullIfEmpty(GetString(el, "mem_perc", "MemPerc")),
                NullIfEmpty(net),
                NullIfEmpty(GetString(el, "block_io", "BlockIO")),
                NullIfEmpty(pids)));
        }
        return list;
    }

    private static string Combine(JsonElement el, string inKey, string outKey)
    {
        var i = GetString(el, inKey);
        var o = GetString(el, outKey);
        return i.Length == 0 && o.Length == 0 ? "" : $"{i} / {o}";
    }

    /// <summary>Object elements of either `[...]` (podman) or JSONL (docker).</summary>
    private static IEnumerable<JsonElement> Elements(string stdout)
    {
        var t = stdout.Trim();
        if (t.Length == 0) yield break;
        if (t.StartsWith('['))
        {
            using var doc = JsonDocument.Parse(t);
            foreach (var el in doc.RootElement.EnumerateArray()) yield return el.Clone();
            yield break;
        }
        foreach (var line in t.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var l = line.Trim();
            if (l.Length == 0) continue;
            using var doc = JsonDocument.Parse(l);
            yield return doc.RootElement.Clone();
        }
    }

    private static JsonElement GetObject(JsonElement el, string name)
        => TryGetObject(el, name, out var v) ? v : default;

    private static bool TryGetObject(JsonElement el, string name, out JsonElement v)
    {
        v = default;
        return el.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.Object;
    }

    private static string GetString(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? "",
                    JsonValueKind.Number => v.ToString(),
                    _ => "",
                };
        return "";
    }

    private static int? TryGetInt(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String &&
                    int.TryParse(v.GetString(), out var i2)) return i2;
            }
        return null;
    }

    private static bool TryGetBool(JsonElement el, params string[] names)
    {
        foreach (var n in names)
            if (el.TryGetProperty(n, out var v))
                return v.ValueKind == JsonValueKind.True ||
                       (v.ValueKind == JsonValueKind.String && v.GetString() == "true");
        return false;
    }

    private static DateTimeOffset? TryGetDate(JsonElement el, params string[] names)
    {
        var s = GetString(el, names);
        return DateTimeOffset.TryParse(s, out var d) && d.Year > 1 ? d : null;
    }

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}
