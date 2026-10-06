using DevStation.Infrastructure.Containers;

namespace DevStation.Tests.Unit.Containers;

public sealed class ContainerOutputParserTests
{
    private const string PodmanPsJson = """
        [
          {
            "Id": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
            "Names": ["web"],
            "Image": "docker.io/library/nginx:latest",
            "State": "running",
            "Status": "Up 2 hours",
            "Ports": [
              {"host_ip": "0.0.0.0", "host_port": 8080, "container_port": 80, "protocol": "tcp"},
              {"container_port": 443, "protocol": "tcp"}
            ]
          },
          {
            "Id": "deadbeef",
            "Names": ["db"],
            "Image": "postgres:16",
            "State": "exited",
            "Status": "Exited (0) 3 days ago",
            "Ports": []
          }
        ]
        """;

    [Fact]
    public void ParseList_reads_podman_json_array()
    {
        var list = ContainerOutputParser.ParseList(PodmanPsJson);
        Assert.Equal(2, list.Count);
        var web = list[0];
        Assert.Equal("web", web.Name);
        Assert.Equal("docker.io/library/nginx:latest", web.Image);
        Assert.Equal("running", web.State);
        Assert.Equal("Up 2 hours", web.Status);
        Assert.Equal(2, web.Ports.Count);
        Assert.Contains("8080", web.Ports[0]);
        Assert.Contains("80", web.Ports[0]);
        Assert.Equal("db", list[1].Name);
        Assert.Equal("exited", list[1].State);
    }

    [Fact]
    public void ParseList_reads_docker_jsonl()
    {
        // docker --format json emits one object per line, not an array
        var jsonl = """
            {"ID":"a1","Names":"web","Image":"nginx","State":"running","Status":"Up 2 hours","Ports":"0.0.0.0:8080->80/tcp"}
            {"ID":"b2","Names":"db","Image":"postgres","State":"exited","Status":"Exited (0) 3 days ago","Ports":""}
            """;
        var list = ContainerOutputParser.ParseList(jsonl);
        Assert.Equal(2, list.Count);
        Assert.Equal("web", list[0].Name);
        Assert.Single(list[0].Ports);
        Assert.Equal("db", list[1].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("  \n  ")]
    public void ParseList_empty_input_gives_empty_list(string json)
        => Assert.Empty(ContainerOutputParser.ParseList(json));

    [Fact]
    public void ParseInspect_extracts_fields_and_filters_env()
    {
        var inspect = """
            [{
              "Id": "a1b2c3",
              "Name": "/web",
              "Config": {
                "Image": "nginx",
                "Env": [
                  "PATH=/usr/bin",
                  "POSTGRES_PASSWORD=hunter2",
                  "api_secret_token=x",
                  "SSH_KEY=/id",
                  "AWS_CREDENTIAL_FILE=/c",
                  "NORMAL_FLAG=1"
                ]
              },
              "State": {"Status": "running", "ExitCode": 0, "StartedAt": "2026-10-01T10:00:00Z"},
              "Created": "2026-10-01T09:00:00Z",
              "Mounts": [
                {"Type": "volume", "Source": "data", "Destination": "/var/lib/data", "RW": true}
              ],
              "NetworkSettings": {
                "Networks": {"bridge": {"IPAddress": "10.88.0.5"}}
              }
            }]
            """;
        var c = ContainerOutputParser.ParseInspect(inspect);
        Assert.Equal("a1b2c3", c.Id);
        Assert.Equal("web", c.Name);
        Assert.Equal("nginx", c.Image);
        Assert.Equal("running", c.State);
        Assert.Equal(0, c.ExitCode);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), c.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), c.CreatedAt);
        Assert.Single(c.Mounts);
        Assert.Equal("/var/lib/data", c.Mounts[0].Destination);
        Assert.Single(c.Networks);
        Assert.Equal("bridge", c.Networks[0].Name);
        Assert.Equal("10.88.0.5", c.Networks[0].IpAddress);

        var env = c.Env.ToList();
        Assert.Equal(2, env.Count);
        Assert.Contains("PATH=/usr/bin", env);
        Assert.Contains("NORMAL_FLAG=1", env);
        Assert.DoesNotContain(env, e => e.Contains("hunter2"));
    }

    [Theory]
    [InlineData("DB_PASSWORD", true)]
    [InlineData("api_secret", true)]
    [InlineData("MY_TOKEN", true)]
    [InlineData("SSH_KEY", true)]
    [InlineData("credential_file", true)]
    [InlineData("MONKEY", true)] // 'key' substring — spec'd regex is substring, not word-boundary
    [InlineData("PATH", false)]
    [InlineData("HOSTNAME", false)]
    [InlineData("NORMAL_FLAG", false)]
    public void EnvFilter_matches_sensitive_names_case_insensitively(string name, bool expected)
        => Assert.Equal(expected, ContainerOutputParser.IsSensitiveEnvName(name));

    [Fact]
    public void ParseStats_reads_podman_array()
    {
        var stats = """
            [{"id":"a1b2c3","name":"web","cpu_percent":"0.42","mem_usage":"12.3MB / 1GB",
              "mem_perc":"1.2","net_input":"1KB","net_output":"2KB","pids":"12"}]
            """;
        var s = ContainerOutputParser.ParseStats(stats);
        Assert.Single(s);
        Assert.Equal("web", s[0].Name);
        Assert.Equal("0.42", s[0].CpuPercent);
        Assert.Equal("12", s[0].Pids);
    }

    [Fact]
    public void ParseStats_reads_docker_jsonl()
    {
        var stats = """
            {"ID":"a1","Name":"web","CPUPerc":"0.42%","MemUsage":"12.3MiB / 1GiB","MemPerc":"1.2%","NetIO":"1kB / 2kB","PIDs":"12"}
            """;
        var s = ContainerOutputParser.ParseStats(stats);
        Assert.Single(s);
        Assert.Equal("0.42%", s[0].CpuPercent);
        Assert.Equal("1kB / 2kB", s[0].NetIo);
    }

    [Fact]
    public async Task NullProvider_lists_empty_and_throws_on_actions()
    {
        var p = NullContainerProvider.Instance;
        Assert.Empty(await p.ListAsync());
        var ex = await Assert.ThrowsAsync<DevStation.Application.Shared.Results.AppException>(
            () => p.StartAsync("abc"));
        Assert.Equal("CONTAINER_RUNTIME_UNAVAILABLE", ex.Code);
        Assert.Equal(503, ex.Status);
        await Assert.ThrowsAsync<DevStation.Application.Shared.Results.AppException>(
            () => p.InspectAsync("abc"));
        await Assert.ThrowsAsync<DevStation.Application.Shared.Results.AppException>(
            () => p.GetLogsAsync("abc", 50));
        await Assert.ThrowsAsync<DevStation.Application.Shared.Results.AppException>(
            () => p.GetStatsAsync("abc"));
    }
}
