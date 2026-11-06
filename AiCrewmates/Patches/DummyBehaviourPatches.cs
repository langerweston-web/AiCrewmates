using AiCrewmates.Bots;
using HarmonyLib;

namespace AiCrewmates.Patches;

/// <summary>
/// Keep DummyBehaviour's built-in random vote from racing our BotBrain votes.
/// When PlayerIdToVoteFor is set (>= -1), DummyBehaviour already uses it in newer builds;
/// this prefix still short-circuits Update for our bots so only BotBrain schedules votes.
/// </summary>
[HarmonyPatch(typeof(DummyBehaviour), nameof(DummyBehaviour.Update))]
public static class DummyBehaviourUpdatePatch
{
    public static bool Prefix(DummyBehaviour __instance)
    {
        if (__instance == null || __instance.myPlayer == null)
        {
            return true;
        }

        // Our bots own meeting logic via BotBrain.
        return !BotManager.IsBot(__instance.myPlayer);
    }
}
