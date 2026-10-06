using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevStation.Application.Shared.Ai;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Ai;

public sealed class AiOptions
{
    /// <summary>ollama | openai | none (default).</summary>
    public string Provider { get; set; } = "none";
    public OllamaOptions Ollama { get; set; } = new();
    public OpenAiOptions OpenAi { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
}

public sealed class OpenAiOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string Model { get; set; } = "gpt-4o-mini";
    /// <summary>Secret-store key holding the API key.</summary>
    public string KeyRef { get; set; } = "ai.openai.key";
}

/// <summary>Picks the configured provider. Shared so unit tests can exercise
/// the selection without spinning up the whole host.</summary>
public static class AiProviderSelector
{
    public static IAiProvider Resolve(IServiceProvider sp, IConfiguration config)
        => (config["Ai:Provider"] ?? "none").Trim().ToLowerInvariant() switch
        {
            "ollama" => sp.GetRequiredService<OllamaProvider>(),
            "openai" => sp.GetRequiredService<OpenAiProvider>(),
            _ => new NullAiProvider(),
        };
}

/// <summary>Shared completion plumbing: POST JSON, extract the text field,
/// map transport failures to AI_UNAVAILABLE.</summary>
public abstract class AiProviderBase(HttpClient http, ILogger log)
    : IAiProvider
{
    protected HttpClient Http { get; } = http;

    public abstract string Name { get; }
    public abstract Task<bool> IsAvailableAsync(CancellationToken ct);
    protected abstract Task<string> SendAsync(string system, string prompt,
        CancellationToken ct);

    public async Task<string> CompleteAsync(string system, string prompt,
        CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(AiLimits.Timeout);
        try
        {
            return await SendAsync(system, prompt, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AppException("AI_UNAVAILABLE", 503,
                $"{Name} provider timed out after {AiLimits.Timeout.TotalSeconds}s");
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "{Provider} request failed", Name);
            throw new AppException("AI_UNAVAILABLE", 503,
                $"{Name} provider unreachable: {ex.Message}");
        }
    }
}

/// <summary>Local Ollama: POST {BaseUrl}/api/generate with stream=false.</summary>
public sealed class OllamaProvider(HttpClient http, IConfiguration config,
    ILogger<OllamaProvider> log) : AiProviderBase(http, log)
{
    private readonly OllamaOptions _o = config.GetSection("Ai:Ollama")
        .Get<OllamaOptions>() ?? new OllamaOptions();

    public override string Name => "ollama";

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var r = await Http.GetAsync(
                $"{_o.BaseUrl}/api/version", ct);
            return r.IsSuccessStatusCode;
        }
        catch (Exception) when (!ct.IsCancellationRequested) { return false; }
    }

    protected override async Task<string> SendAsync(string system,
        string prompt, CancellationToken ct)
    {
        var res = await Http.PostAsJsonAsync($"{_o.BaseUrl}/api/generate",
            new { model = _o.Model, system, prompt, stream = false }, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<OllamaResponse>(ct);
        return body?.Response ?? "";
    }

    private sealed record OllamaResponse(
        [property: JsonPropertyName("response")] string Response);
}

/// <summary>OpenAI chat completions; key resolved per-call from ISecretStore
/// (<c>ai.openai.key</c>) so it never lands in config files.</summary>
public sealed class OpenAiProvider(HttpClient http, IConfiguration config,
    ISecretStore secrets, ILogger<OpenAiProvider> log)
    : AiProviderBase(http, log)
{
    private readonly OpenAiOptions _o = config.GetSection("Ai:OpenAi")
        .Get<OpenAiOptions>() ?? new OpenAiOptions();

    public override string Name => "openai";

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
        => !string.IsNullOrEmpty(await secrets.GetAsync(_o.KeyRef, ct));

    protected override async Task<string> SendAsync(string system,
        string prompt, CancellationToken ct)
    {
        var key = await secrets.GetAsync(_o.KeyRef, ct)
            ?? throw new AppException("AI_UNAVAILABLE", 503,
                "OpenAI key missing (secret 'ai.openai.key' not set)");
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{_o.BaseUrl}/v1/chat/completions");
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        req.Content = JsonContent.Create(new
        {
            model = _o.Model,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = prompt },
            },
        });
        var res = await Http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ChatResponse>(ct);
        return body?.Choices is [{ } first, ..] ? first.Message.Content ?? "" : "";
    }

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] Choice[] Choices);
    private sealed record Choice(
        [property: JsonPropertyName("message")] ChatMessage Message);
    private sealed record ChatMessage(
        [property: JsonPropertyName("content")] string? Content);
}
