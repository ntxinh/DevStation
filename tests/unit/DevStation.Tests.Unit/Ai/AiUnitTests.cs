using DevStation.Application.Modules.Ai;
using DevStation.Application.Shared.Ai;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevStation.Tests.Unit.Ai;

public class AiProviderSelectionTests
{
    private sealed class EmptySecrets : ISecretStore
    {
        public Task SetAsync(string k, string v, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string k, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task DeleteAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static IServiceProvider Sp(IConfiguration config)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton<ISecretStore, EmptySecrets>();
        s.AddSingleton(config);
        s.AddHttpClient<OllamaProvider>();
        s.AddHttpClient<OpenAiProvider>();
        return s.BuildServiceProvider();
    }

    private static IConfiguration Cfg(string? provider)
        => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Ai:Provider"] = provider }).Build();

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("garbage")]
    public void Defaults_to_null_provider(string? provider)
    {
        var p = AiProviderSelector.Resolve(Sp(Cfg(provider)), Cfg(provider));
        Assert.IsType<NullAiProvider>(p);
    }

    [Fact]
    public async Task Null_provider_throws_503()
    {
        var p = new NullAiProvider();
        Assert.False(await p.IsAvailableAsync(default));
        var ex = await Assert.ThrowsAsync<AppException>(
            () => p.CompleteAsync("s", "x", default));
        Assert.Equal(503, ex.Status);
        Assert.Equal("AI_UNAVAILABLE", ex.Code);
    }

    [Theory]
    [InlineData("ollama", typeof(OllamaProvider))]
    [InlineData("openai", typeof(OpenAiProvider))]
    [InlineData("OLLAMA", typeof(OllamaProvider))]
    public void Resolves_configured_provider(string provider, Type expected)
    {
        var cfg = Cfg(provider);
        Assert.IsType(expected, AiProviderSelector.Resolve(Sp(cfg), cfg));
    }
}

public class AiJsonTests
{
    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("Here you go:\n```\n{\"a\":1}\n```\nhope it helps", "{\"a\":1}")]
    [InlineData("prefix {\"a\":{\"b\":2}} suffix", "{\"a\":{\"b\":2}}")]
    [InlineData("no json at all", null)]
    public void Extracts_json_block(string raw, string? expected)
        => Assert.Equal(expected, AiJson.ExtractJsonBlock(raw));

    private sealed record Payload(string? Cause);

    [Fact]
    public void Parse_reads_fenced_object()
    {
        var p = AiJson.Parse<Payload>("```json\n{\"cause\":\"x\"}\n```");
        Assert.Equal("x", p?.Cause);
    }

    [Fact]
    public void Parse_returns_null_for_plain_text()
        => Assert.Null(AiJson.Parse<Payload>("the sky is blue"));
}

public class AiPromptsTests
{
    [Fact]
    public void Prompts_truncate_input_to_8k()
    {
        var huge = new string('x', AiLimits.MaxInputChars + 500);
        var prompt = AiPrompts.ExplainErrorUser(huge);
        Assert.DoesNotContain(huge, prompt);
        Assert.True(prompt.Length < huge.Length + 200);
    }

    [Fact]
    public void Explain_sql_appends_schema_when_present()
    {
        var with = AiPrompts.ExplainSqlUser("select 1", "schema here");
        var without = AiPrompts.ExplainSqlUser("select 1", null);
        Assert.Contains("schema here", with);
        Assert.DoesNotContain("Schema:", without);
    }

    [Fact]
    public void Generate_sql_asks_for_read_only_json()
        => Assert.Contains("SELECT", AiPrompts.GenerateSqlSystem);

    [Fact]
    public void Schema_summary_lists_tables_and_columns()
    {
        var schema = new DatabaseSchema([
            new SchemaInfo("public", [
                new TableInfo("projects",
                    [new ColumnInfo("id", "uuid", false, true),
                     new ColumnInfo("name", "text", false, false)], []),
            ], [], []),
        ]);
        var s = AiPrompts.SchemaSummary("db1", schema);
        Assert.Contains("public.projects(", s);
        Assert.Contains("id:uuid*", s); // pk flagged
        Assert.Contains("name:text", s);
    }
}
