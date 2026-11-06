using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiCrewmates.Bots;

namespace AiCrewmates.AI;

public sealed class OpenAiMeetingDecision
{
    public string? Chat { get; set; }
    public byte? VotePlayerId { get; set; }
    public bool Skip { get; set; }
}

/// <summary>
/// Optional OpenAI-compatible chat completions client for meeting talk + votes.
/// Configured via BepInEx config; never hardcodes a key.
/// </summary>
public static class OpenAiChatClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    public static async Task<OpenAiMeetingDecision?> DecideAsync(
        string botName,
        string botColor,
        bool isImpostor,
        BotMemory memory,
        IReadOnlyList<(byte Id, string Name, string Color)> living,
        CancellationToken ct)
    {
        if (!ModConfig.HasLiveAi)
        {
            return null;
        }

        var baseUrl = ModConfig.OpenAiBaseUrl.Value.Trim().TrimEnd('/');
        var model = ModConfig.OpenAiModel.Value.Trim();
        var key = ModConfig.OpenAiApiKey.Value.Trim();

        var livingText = string.Join(", ", living.Select(p => $"{p.Color}/{p.Name}(id={p.Id})"));
        var system = """
            You are an Among Us player in a meeting. Reply with compact JSON only:
            {"chat":"short message or empty","vote":"<playerId or skip>"}
            Keep chat under 80 characters. Use your memory. Do not admit to modding.
            """;
        var user = $"""
            You are {botColor} named {botName}. Role hidden tip: {(isImpostor ? "impostor" : "crewmate")}.
            Living players: {livingText}
            Memory:
            {memory.SummarizeForPrompt()}
            """;

        var payload = new
        {
            model,
            temperature = 0.7,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                AiCrewmatesPlugin.Instance.Log.LogWarning($"OpenAI HTTP {(int)resp.StatusCode}: {Truncate(body, 200)}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return ParseDecision(content, living);
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogWarning($"OpenAI request failed: {ex.Message}");
            return null;
        }
    }

    private static OpenAiMeetingDecision? ParseDecision(
        string? content,
        IReadOnlyList<(byte Id, string Name, string Color)> living)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var json = content.Trim();
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return new OpenAiMeetingDecision { Chat = Truncate(json, 80), Skip = true };
        }

        json = json[start..(end + 1)];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var chat = root.TryGetProperty("chat", out var c) ? c.GetString() : null;
            var vote = root.TryGetProperty("vote", out var v) ? v.GetString() : "skip";
            var decision = new OpenAiMeetingDecision
            {
                Chat = string.IsNullOrWhiteSpace(chat) ? null : Truncate(chat!, 80),
            };

            if (string.IsNullOrWhiteSpace(vote) || vote.Equals("skip", StringComparison.OrdinalIgnoreCase))
            {
                decision.Skip = true;
                return decision;
            }

            if (byte.TryParse(vote, out var id) && living.Any(p => p.Id == id))
            {
                decision.VotePlayerId = id;
                return decision;
            }

            var match = living.FirstOrDefault(p =>
                p.Name.Equals(vote, StringComparison.OrdinalIgnoreCase) ||
                p.Color.Equals(vote, StringComparison.OrdinalIgnoreCase));
            if (match.Name != null)
            {
                decision.VotePlayerId = match.Id;
            }
            else
            {
                decision.Skip = true;
            }

            return decision;
        }
        catch
        {
            return new OpenAiMeetingDecision { Chat = Truncate(content, 80), Skip = true };
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
