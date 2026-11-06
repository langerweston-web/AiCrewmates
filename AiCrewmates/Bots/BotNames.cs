namespace AiCrewmates.Bots;

public static class BotNames
{
    private static readonly string[] Pool =
    {
        "Ada", "Byte", "Circuit", "Delta", "Echo", "Flux", "Gizmo", "Helix",
        "Ion", "Jolt", "Kernel", "Logic", "Mod", "Nova", "Otto", "Pulse",
        "Quark", "Relay", "Spark", "Torque", "Unit", "Vox", "Watt", "Xeno",
        "Yotta", "Zed", "Pixel", "Nexus", "Rook", "Sable",
    };

    private static readonly System.Random Rng = new();

    public static string Next(HashSet<string> taken)
    {
        foreach (var name in Pool.OrderBy(_ => Rng.Next()))
        {
            if (!taken.Contains(name))
            {
                return name;
            }
        }

        return $"Bot{Rng.Next(100, 999)}";
    }

    public static HashSet<string> TakenNames()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (GameData.Instance?.AllPlayers == null)
        {
            return taken;
        }

        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (!string.IsNullOrWhiteSpace(info?.PlayerName))
            {
                taken.Add(info.PlayerName);
            }
        }

        return taken;
    }
}
