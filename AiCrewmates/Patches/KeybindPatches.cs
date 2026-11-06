using AiCrewmates.Bots;
using HarmonyLib;
using UnityEngine;

namespace AiCrewmates.Patches;

/// <summary>
/// Host keybinds in lobby: F6 add bot, F7 remove bot.
/// </summary>
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
public static class KeybindPatches
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost || !GameHelpersSafe())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            Notify(BotManager.AddBot());
        }
        else if (Input.GetKeyDown(KeyCode.F7))
        {
            Notify(BotManager.RemoveBot());
        }
    }

    private static bool GameHelpersSafe()
    {
        try
        {
            return Util.GameHelpers.IsBotFriendlySession();
        }
        catch
        {
            return false;
        }
    }

    private static void Notify(string msg)
    {
        AiCrewmatesPlugin.Instance.Log.LogInfo(msg);
        try
        {
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, $"[AiCrewmates] {msg}", false);
        }
        catch
        {
            // Chat may be unavailable.
        }
    }
}
