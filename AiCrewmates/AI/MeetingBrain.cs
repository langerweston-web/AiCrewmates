using AiCrewmates.Bots;
using AiCrewmates.Util;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AiCrewmates.AI;

public static class MeetingBrain
{
    public static string? BuildRuleChat(BotMemory memory, bool isImpostor, string selfColor)
    {
        if (!ModConfig.ChatEnabled.Value)
        {
            return null;
        }

        var interesting = memory.LatestInteresting();
        if (memory.LastBodyReporterNote != null && Random.value < 0.7f)
        {
            var note = memory.LastBodyReporterNote;
            memory.LastBodyReporterNote = null;
            return note;
        }

        if (interesting != null)
        {
            if (interesting.WasNearBody)
            {
                return $"I saw {interesting.Color} near the body in {GameHelpers.RoomName(interesting.Room)}";
            }

            if (interesting.WasInVent)
            {
                return $"I saw {interesting.Color} vent in {GameHelpers.RoomName(interesting.Room)}";
            }

            if (Random.value < 0.55f)
            {
                return $"I saw {interesting.Color} near {GameHelpers.RoomName(interesting.Room)}";
            }
        }

        if (isImpostor)
        {
            return Random.value < 0.4f ? "I was doing tasks, didn't see much" : null;
        }

        return Random.value < 0.25f ? $"{selfColor} clear? I was on tasks" : null;
    }

    public static byte DecideRuleVote(BotMemory memory, PlayerControl self)
    {
        // 253 = skip in classic CmdCastVote encoding used by DummyBehaviour (-1 -> 253/255 depending on build).
        const byte Skip = 253;
        var living = GameHelpers.LivingPlayers()
            .Where(p => p.PlayerId != self.PlayerId)
            .Select(p => p.PlayerId)
            .ToList();

        var suspect = memory.MostSuspicious(living, self.PlayerId);
        if (suspect.HasValue)
        {
            return suspect.Value;
        }

        // Mild impostor self-preservation: randomly push suspicion onto a crewmate sometimes.
        if (GameHelpers.IsImpostor(self) && living.Count > 0 && Random.value < 0.35f)
        {
            return living[Random.Range(0, living.Count)];
        }

        return Skip;
    }

    public static void ObserveChatAccusation(BotMemory memory, PlayerControl speaker, string text, PlayerControl self)
    {
        if (speaker == null || self == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var lower = text.ToLowerInvariant();
        var selfColor = GameHelpers.ColorName(self).ToLowerInvariant();
        var selfName = self.Data?.PlayerName?.ToLowerInvariant() ?? "";

        if ((!string.IsNullOrEmpty(selfColor) && lower.Contains(selfColor)) ||
            (!string.IsNullOrEmpty(selfName) && lower.Contains(selfName)))
        {
            memory.ReactToAccusation(self.PlayerId, speaker.PlayerId, againstSelf: true);
            return;
        }

        foreach (var other in GameHelpers.LivingPlayers())
        {
            if (other.PlayerId == self.PlayerId)
            {
                continue;
            }

            var color = GameHelpers.ColorName(other).ToLowerInvariant();
            var name = other.Data?.PlayerName?.ToLowerInvariant() ?? "";
            if ((color.Length > 2 && lower.Contains(color)) || (name.Length > 2 && lower.Contains(name)))
            {
                memory.ReactToAccusation(other.PlayerId, speaker.PlayerId, againstSelf: false);
                break;
            }
        }
    }
}
