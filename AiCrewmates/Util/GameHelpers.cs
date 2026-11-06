using AmongUs.GameOptions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AiCrewmates.Util;

public static class GameHelpers
{
    public static bool IsHost =>
        AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    public static bool IsBotFriendlySession()
    {
        var client = AmongUsClient.Instance;
        if (client == null)
        {
            return false;
        }

        if (!ModConfig.AllowOnlyPrivateOrLocal.Value)
        {
            return client.AmHost;
        }

        // Freeplay and local always OK. Online only when the lobby is not public.
        return client.NetworkMode switch
        {
            NetworkModes.FreePlay => true,
            NetworkModes.LocalGame => true,
            NetworkModes.OnlineGame => !client.IsGamePublic,
            _ => false,
        };
    }

    public static bool CanActInRound(PlayerControl? player)
    {
        if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected)
        {
            return false;
        }

        if (MeetingHud.Instance != null || ExileController.Instance != null || IntroCutscene.Instance != null)
        {
            return false;
        }

        if (LobbyBehaviour.Instance != null || ShipStatus.Instance == null)
        {
            return false;
        }

        return player.moveable;
    }

    public static string ColorName(byte colorId)
    {
        try
        {
            return Palette.GetColorName(colorId);
        }
        catch
        {
            return $"Color{colorId}";
        }
    }

    public static string ColorName(PlayerControl player)
    {
        if (player?.Data?.DefaultOutfit == null)
        {
            return player?.Data?.PlayerName ?? "Unknown";
        }

        return ColorName((byte)player.Data.DefaultOutfit.ColorId);
    }

    public static string RoomName(SystemTypes room)
    {
        try
        {
            return TranslationController.Instance != null
                ? TranslationController.Instance.GetString(room)
                : room.ToString();
        }
        catch
        {
            return room.ToString();
        }
    }

    public static SystemTypes GuessRoom(Vector2 position)
    {
        var ship = ShipStatus.Instance;
        if (ship == null)
        {
            return SystemTypes.Hallway;
        }

        try
        {
            if (ship.FastRooms != null)
            {
                foreach (var entry in ship.FastRooms)
                {
                    var room = entry.Value;
                    if (room == null || room.roomArea == null)
                    {
                        continue;
                    }

                    if (room.roomArea.OverlapPoint(position))
                    {
                        return entry.Key;
                    }
                }
            }
        }
        catch
        {
            // Fall through to nearest console heuristic.
        }

        SystemTypes best = SystemTypes.Hallway;
        var bestDist = float.MaxValue;
        if (ship.AllConsoles != null)
        {
            foreach (var console in ship.AllConsoles)
            {
                if (console == null)
                {
                    continue;
                }

                var d = Vector2.Distance(position, console.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = console.Room;
                }
            }
        }

        return best;
    }

    public static float KillRange()
    {
        // Kill distance option enums vary by build; use a safe mid-range default.
        return 1.8f;
    }

    public static bool IsImpostor(PlayerControl? player)
    {
        if (player?.Data?.Role == null)
        {
            return false;
        }

        try
        {
            return player.Data.Role.IsImpostor;
        }
        catch
        {
            return RoleManager.IsImpostorRole(player.Data.RoleType);
        }
    }

    public static IEnumerable<PlayerControl> LivingPlayers()
    {
        if (PlayerControl.AllPlayerControls == null)
        {
            yield break;
        }

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player != null && player.Data != null && !player.Data.IsDead && !player.Data.Disconnected)
            {
                yield return player;
            }
        }
    }

    public static DeadBody[] FindBodies()
    {
        try
        {
            return Object.FindObjectsOfType<DeadBody>();
        }
        catch
        {
            return Array.Empty<DeadBody>();
        }
    }

    public static byte NextFreeColorId(HashSet<byte> used)
    {
        var count = Palette.PlayerColors != null ? Palette.PlayerColors.Count : 18;
        for (byte i = 0; i < count; i++)
        {
            if (!used.Contains(i))
            {
                return i;
            }
        }

        return (byte)(used.Count % Math.Max(1, count));
    }

    public static HashSet<byte> UsedColorIds()
    {
        var used = new HashSet<byte>();
        if (GameData.Instance?.AllPlayers == null)
        {
            return used;
        }

        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info?.DefaultOutfit != null)
            {
                used.Add((byte)info.DefaultOutfit.ColorId);
            }
        }

        return used;
    }
}
