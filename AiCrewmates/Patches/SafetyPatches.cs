using AiCrewmates.Bots;
using HarmonyLib;
using UnityEngine;

namespace AiCrewmates.Patches;

/// <summary>
/// Soft-guard: poll for public lobbies and strip bots so we never participate in public MM.
/// </summary>
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
public static class PublicLobbySafetyPatch
{
    private static float _nextCheck;

    public static void Postfix()
    {
        if (Time.time < _nextCheck)
        {
            return;
        }

        _nextCheck = Time.time + 1.5f;
        var client = AmongUsClient.Instance;
        if (client == null || !ModConfig.AllowOnlyPrivateOrLocal.Value || BotManager.Count == 0)
        {
            return;
        }

        if (client.IsGamePublic)
        {
            AiCrewmatesPlugin.Instance.Log.LogWarning("Lobby is public — removing AI bots.");
            BotManager.ClearAll();
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
public static class ShipStatusBeginPatch
{
    public static void Postfix()
    {
        AI.Pathfinding.Invalidate();
        BotManager.OnGameStarted();
    }
}
