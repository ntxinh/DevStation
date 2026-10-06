using System.Text.Json.Nodes;
using DevStation.Application.Shared.Auditing;

namespace DevStation.Tests.Unit.Audit;

public class RedactionTests
{
    [Fact]
    public void Null_and_empty_metadata_stay_null()
    {
        Assert.Null(AuditRedactor.Redact(null));
        Assert.Null(AuditRedactor.Redact(""));
    }

    [Fact]
    public void Sensitive_keys_are_masked_case_insensitively()
    {
        var json = """{"password":"hunter2","Name":"ok","API_KEY":"abc","clientSecret":"s","access_token":"t"}""";
        var node = AuditRedactor.Redact(json)!;
        Assert.Equal("***", node["password"]!.GetValue<string>());
        Assert.Equal("***", node["API_KEY"]!.GetValue<string>());
        Assert.Equal("***", node["clientSecret"]!.GetValue<string>());
        Assert.Equal("***", node["access_token"]!.GetValue<string>());
        Assert.Equal("ok", node["Name"]!.GetValue<string>());
    }

    [Fact]
    public void Nested_objects_and_arrays_are_redacted()
    {
        var json = """{"outer":{"dbPassword":"x","note":"y"},"list":[{"token":"z"}]}""";
        var node = AuditRedactor.Redact(json)!;
        Assert.Equal("***", node["outer"]!["dbPassword"]!.GetValue<string>());
        Assert.Equal("y", node["outer"]!["note"]!.GetValue<string>());
        Assert.Equal("***", node["list"]![0]!["token"]!.GetValue<string>());
    }

    [Fact]
    public void Invalid_json_returns_null_rather_than_leaking()
        => Assert.Null(AuditRedactor.Redact("not json {"));
}
