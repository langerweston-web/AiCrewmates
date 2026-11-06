using AiCrewmates.AI;
using AiCrewmates.Bots;
using HarmonyLib;

namespace AiCrewmates.Patches;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
public static class ChatCommandPatches
{
    public static bool Prefix(PlayerControl sourcePlayer, string chatText)
    {
        if (sourcePlayer == null || string.IsNullOrWhiteSpace(chatText))
        {
            return true;
        }

        // Feed accusations into bot memories during meetings.
        if (MeetingHud.Instance != null)
        {
            foreach (var bot in BotManager.All)
            {
                if (bot?.Player == null || bot.Player.PlayerId == sourcePlayer.PlayerId)
                {
                    continue;
                }

                MeetingBrain.ObserveChatAccusation(bot.Memory, sourcePlayer, chatText, bot.Player);
            }
        }

        if (!chatText.StartsWith('/'))
        {
            return true;
        }

        // Only the local host's typed commands mutate bots.
        if (!AmongUsClient.Instance.AmHost || sourcePlayer != PlayerControl.LocalPlayer)
        {
            return true;
        }

        var parts = chatText.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : null;

        string? response = cmd switch
        {
            "/addbot" => BotManager.AddBot(arg),
            "/removebot" => BotManager.RemoveBot(arg),
            "/clearbots" => Clear(),
            "/bots" => $"Bots online: {BotManager.Count}/{ModConfig.MaxBots.Value}",
            "/fillbots" => int.TryParse(arg, out var n) ? BotManager.FillBots(n) : "Usage: /fillbots <count>",
            _ => null,
        };

        if (response == null)
        {
            return true;
        }

        HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, $"[AiCrewmates] {response}", false);
        AiCrewmatesPlugin.Instance.Log.LogInfo(response);
        // Swallow the command so it doesn't spam as normal chat.
        return false;
    }

    private static string Clear()
    {
        BotManager.ClearAll();
        return "Cleared all bots.";
    }
}
