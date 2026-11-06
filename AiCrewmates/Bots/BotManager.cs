using AiCrewmates.AI;
using AiCrewmates.Util;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AiCrewmates.Bots;

public static class BotManager
{
    private static readonly List<BotBrain> Bots = new();
    private static int _nextFakeClientId = 17000;

    public static IReadOnlyList<BotBrain> All => Bots;
    public static int Count => Bots.Count;

    public static string AddBot(string? preferredName = null)
    {
        if (!GameHelpers.IsHost)
        {
            return "Only the host can add bots.";
        }

        if (!GameHelpers.IsBotFriendlySession())
        {
            return "Bots are only allowed in freeplay, local, or private lobbies (not public matchmaking).";
        }

        if (AmongUsClient.Instance == null || GameData.Instance == null)
        {
            return "Join a lobby first.";
        }

        if (Bots.Count >= ModConfig.MaxBots.Value)
        {
            return $"Bot limit reached ({ModConfig.MaxBots.Value}).";
        }

        if (GameData.Instance.PlayerCount >= 15)
        {
            return "Lobby is full.";
        }

        try
        {
            var name = string.IsNullOrWhiteSpace(preferredName)
                ? BotNames.Next(BotNames.TakenNames())
                : preferredName.Trim();
            var color = GameHelpers.NextFreeColorId(GameHelpers.UsedColorIds());
            var bot = SpawnBot(name, color);
            if (bot == null)
            {
                return "Failed to spawn bot (see BepInEx log).";
            }

            return $"Added bot {name} ({GameHelpers.ColorName(color)}). Bots: {Bots.Count}/{ModConfig.MaxBots.Value}";
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogError($"AddBot failed: {ex}");
            return $"AddBot error: {ex.Message}";
        }
    }

    public static string RemoveBot(string? nameOrColor = null)
    {
        if (!GameHelpers.IsHost)
        {
            return "Only the host can remove bots.";
        }

        CleanupNulls();
        if (Bots.Count == 0)
        {
            return "No bots to remove.";
        }

        BotBrain? target = null;
        if (!string.IsNullOrWhiteSpace(nameOrColor))
        {
            var key = nameOrColor.Trim();
            target = Bots.FirstOrDefault(b =>
                b.Player?.Data?.PlayerName != null &&
                b.Player.Data.PlayerName.Equals(key, StringComparison.OrdinalIgnoreCase));

            target ??= Bots.FirstOrDefault(b =>
                b.Player != null &&
                GameHelpers.ColorName(b.Player).Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        target ??= Bots[^1];
        var label = target.Player?.Data?.PlayerName ?? "bot";
        DespawnBot(target);
        return $"Removed {label}. Bots: {Bots.Count}";
    }

    public static string FillBots(int desiredTotal)
    {
        desiredTotal = Mathf.Clamp(desiredTotal, 0, ModConfig.MaxBots.Value);
        var messages = new List<string>();
        while (Bots.Count < desiredTotal)
        {
            var msg = AddBot();
            messages.Add(msg);
            if (!msg.StartsWith("Added", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        while (Bots.Count > desiredTotal)
        {
            messages.Add(RemoveBot());
        }

        return messages.Count == 0 ? $"Bots: {Bots.Count}" : string.Join(" | ", messages.TakeLast(3));
    }

    public static void ClearAll()
    {
        foreach (var bot in Bots.ToList())
        {
            DespawnBot(bot);
        }

        Bots.Clear();
        Pathfinding.Invalidate();
    }

    public static bool IsBot(PlayerControl? player)
    {
        if (player == null)
        {
            return false;
        }

        return Bots.Any(b => b != null && b.Player != null && b.Player.PlayerId == player.PlayerId);
    }

    public static BotBrain? GetBrain(PlayerControl? player)
    {
        if (player == null)
        {
            return null;
        }

        return Bots.FirstOrDefault(b => b != null && b.Player != null && b.Player.PlayerId == player.PlayerId);
    }

    public static void NotifyDestroyed(BotBrain brain)
    {
        Bots.Remove(brain);
    }

    public static void OnGameStarted()
    {
        Pathfinding.Invalidate();
        foreach (var bot in Bots)
        {
            bot.Memory.ClearRound();
        }
    }

    private static BotBrain? SpawnBot(string name, byte colorId)
    {
        var auc = AmongUsClient.Instance;
        var prefab = auc.PlayerPrefab;
        if (prefab == null)
        {
            AiCrewmatesPlugin.Instance.Log.LogError("PlayerPrefab is null.");
            return null;
        }

        var availableId = GameData.Instance.GetAvailableId();
        if (availableId < 0)
        {
            AiCrewmatesPlugin.Instance.Log.LogError("No available player id.");
            return null;
        }

        var clientId = _nextFakeClientId++;
        var platform = new PlatformSpecificData
        {
            Platform = Platforms.StandaloneSteamPC,
            PlatformName = "AiCrewmates",
        };
        var client = new ClientData(clientId, name, platform, 1, $"aicrew-{clientId}", $"bot{clientId}");
        client.InScene = true;
        client.IsReady = true;
        client.ColorId = colorId;

        // Keep the fake client in the host's client list so AddPlayer / ownership paths succeed.
        try
        {
            if (auc.allClients != null && !auc.allClients.Contains(client))
            {
                auc.allClients.Add(client);
            }
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogWarning($"Could not register fake ClientData: {ex.Message}");
        }

        Vector2 spawnPos = Vector2.zero;
        if (LobbyBehaviour.Instance != null && LobbyBehaviour.Instance.SpawnPositions != null &&
            LobbyBehaviour.Instance.SpawnPositions.Length > 0)
        {
            var positions = LobbyBehaviour.Instance.SpawnPositions;
            spawnPos = positions[(byte)availableId % positions.Length];
        }
        else if (ShipStatus.Instance != null)
        {
            // Offset around the host / map origin — spawn APIs vary by build.
            var origin = PlayerControl.LocalPlayer != null
                ? PlayerControl.LocalPlayer.GetTruePosition()
                : (Vector2)ShipStatus.Instance.transform.position;
            var angle = availableId * (360f / 15f) * Mathf.Deg2Rad;
            spawnPos = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.8f;
        }

        var player = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
        player.PlayerId = (byte)availableId;
        player.isNew = true;
        player.notRealPlayer = true;

        NetworkedPlayerInfo? info;
        try
        {
            info = GameData.Instance.AddPlayer(player, client);
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogError($"GameData.AddPlayer failed: {ex}");
            Object.Destroy(player.gameObject);
            return null;
        }

        client.Character = player;

        try
        {
            auc.Spawn(player, clientId, SpawnFlags.IsClientCharacter);
        }
        catch
        {
            // Some builds prefer owner -2 for host-simulated dummies.
            auc.Spawn(player, -2, SpawnFlags.None);
        }

        try
        {
            player.RpcSetName(name);
            player.RpcSetColor(colorId);
        }
        catch
        {
            player.RawSetName(name);
            player.RawSetColor(colorId);
            if (info != null)
            {
                info.PlayerName = name;
                if (info.DefaultOutfit != null)
                {
                    info.DefaultOutfit.ColorId = colorId;
                    info.DefaultOutfit.PlayerName = name;
                }
            }
        }

        // DummyBehaviour gives the game a known "dummy" path for voting; we override vote target.
        var dummy = player.gameObject.GetComponent<DummyBehaviour>();
        if (dummy == null)
        {
            dummy = player.gameObject.AddComponent<DummyBehaviour>();
        }

        var brain = player.gameObject.AddComponent<BotBrain>();
        brain.Init(player);
        Bots.Add(brain);

        AiCrewmatesPlugin.Instance.Log.LogInfo($"Spawned bot '{name}' id={player.PlayerId} color={colorId}");
        return brain;
    }

    private static void DespawnBot(BotBrain brain)
    {
        if (brain == null)
        {
            return;
        }

        var player = brain.Player;
        Bots.Remove(brain);

        if (player == null)
        {
            return;
        }

        try
        {
            if (AmongUsClient.Instance != null)
            {
                AmongUsClient.Instance.Despawn(player);
            }
        }
        catch
        {
            // Fall through to destroy.
        }

        try
        {
            Object.Destroy(player.gameObject);
        }
        catch
        {
            // ignore
        }
    }

    private static void CleanupNulls()
    {
        Bots.RemoveAll(b => b == null || b.Player == null);
    }
}
