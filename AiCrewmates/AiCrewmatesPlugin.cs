using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor;
using Reactor.Networking.Attributes;

namespace AiCrewmates;

/// <summary>
/// AI bots for local / freeplay / private Among Us lobbies.
/// Targeted Among Us build: Steam 17.4.x (GameLibs 2026.9.29) with Reactor 2.5.1.
/// </summary>
[BepInPlugin(Id, "AiCrewmates", Version)]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[ReactorModFlags(Reactor.Networking.ModFlags.RequireOnAllClients)]
public partial class AiCrewmatesPlugin : BasePlugin
{
    public const string Id = "com.weston.aicrewmates";
    public const string Version = "1.0.0";

    public static AiCrewmatesPlugin Instance { get; private set; } = null!;
    public Harmony Harmony { get; } = new(Id);

    public override void Load()
    {
        Instance = this;
        ModConfig.Bind(Config);
        Harmony.PatchAll();
        Log.LogInfo($"AiCrewmates {Version} loaded (target AU GameLibs 2026.9.29 / 17.4.x).");
    }
}
