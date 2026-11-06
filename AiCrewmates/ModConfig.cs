using BepInEx.Configuration;

namespace AiCrewmates;

public static class ModConfig
{
    public static ConfigEntry<int> MaxBots { get; private set; } = null!;
    public static ConfigEntry<float> ReactionTimeSeconds { get; private set; } = null!;
    public static ConfigEntry<float> KillAggressiveness { get; private set; } = null!;
    public static ConfigEntry<bool> ChatEnabled { get; private set; } = null!;
    public static ConfigEntry<float> TaskCompleteDelaySeconds { get; private set; } = null!;
    public static ConfigEntry<bool> AllowOnlyPrivateOrLocal { get; private set; } = null!;

    public static ConfigEntry<string> OpenAiBaseUrl { get; private set; } = null!;
    public static ConfigEntry<string> OpenAiApiKey { get; private set; } = null!;
    public static ConfigEntry<string> OpenAiModel { get; private set; } = null!;

    public static bool HasLiveAi =>
        !string.IsNullOrWhiteSpace(OpenAiBaseUrl.Value) &&
        !string.IsNullOrWhiteSpace(OpenAiApiKey.Value) &&
        !string.IsNullOrWhiteSpace(OpenAiModel.Value);

    public static void Bind(ConfigFile config)
    {
        MaxBots = config.Bind(
            "Bots",
            "MaxBots",
            10,
            "Maximum number of AI bots the host may add to a lobby.");

        ReactionTimeSeconds = config.Bind(
            "Difficulty",
            "ReactionTimeSeconds",
            1.25f,
            "Base delay before bots react (report, chat, vote, task start). Lower = harder / snappier.");

        KillAggressiveness = config.Bind(
            "Difficulty",
            "KillAggressiveness",
            0.55f,
            "0..1 chance an impostor bot attempts a kill when conditions are good.");

        TaskCompleteDelaySeconds = config.Bind(
            "Difficulty",
            "TaskCompleteDelaySeconds",
            3.5f,
            "How long a crewmate bot waits at a task console before completing it.");

        ChatEnabled = config.Bind(
            "Chat",
            "Enabled",
            true,
            "When true, bots post short messages in meetings.");

        AllowOnlyPrivateOrLocal = config.Bind(
            "Safety",
            "AllowOnlyPrivateOrLocal",
            true,
            "Block bot spawning outside freeplay / local / private lobbies. Never joins public matchmaking.");

        OpenAiBaseUrl = config.Bind(
            "OpenAI",
            "BaseUrl",
            "",
            "Optional OpenAI-compatible chat completions base URL, e.g. https://api.openai.com/v1. Leave empty for rule-based chat/votes.");

        OpenAiApiKey = config.Bind(
            "OpenAI",
            "ApiKey",
            "",
            "Optional API key for the OpenAI-compatible endpoint. Never commit a real key.");

        OpenAiModel = config.Bind(
            "OpenAI",
            "Model",
            "",
            "Optional model name, e.g. gpt-4o-mini. Required together with BaseUrl and ApiKey to enable live AI chat.");
    }
}
