using AiCrewmates.Util;
using UnityEngine;

namespace AiCrewmates.Bots;

public sealed class Sighting
{
    public byte PlayerId { get; init; }
    public string PlayerName { get; init; } = "";
    public string Color { get; init; } = "";
    public SystemTypes Room { get; init; }
    public Vector2 Position { get; init; }
    public float GameTime { get; init; }
    public bool WasNearBody { get; init; }
    public bool WasInVent { get; init; }
}

public sealed class BotMemory
{
    private readonly List<Sighting> _sightings = new();
    private readonly Dictionary<byte, float> _suspicion = new();

    public IReadOnlyList<Sighting> Sightings => _sightings;
    public string? LastBodyReporterNote { get; set; }

    public void ClearRound()
    {
        _sightings.Clear();
        _suspicion.Clear();
        LastBodyReporterNote = null;
    }

    public void Observe(PlayerControl self, IEnumerable<PlayerControl> others, float visionRange)
    {
        if (self == null)
        {
            return;
        }

        var myPos = self.GetTruePosition();
        var now = Time.time;
        foreach (var other in others)
        {
            if (other == null || other.PlayerId == self.PlayerId || other.Data == null || other.Data.IsDead)
            {
                continue;
            }

            var dist = Vector2.Distance(myPos, other.GetTruePosition());
            if (dist > visionRange)
            {
                continue;
            }

            var room = GameHelpers.GuessRoom(other.GetTruePosition());
            _sightings.Add(new Sighting
            {
                PlayerId = other.PlayerId,
                PlayerName = other.Data.PlayerName,
                Color = GameHelpers.ColorName(other),
                Room = room,
                Position = other.GetTruePosition(),
                GameTime = now,
                WasInVent = other.inVent,
            });

            // Keep memory bounded.
            if (_sightings.Count > 64)
            {
                _sightings.RemoveAt(0);
            }
        }
    }

    public void NoteBodyNear(PlayerControl? suspect, SystemTypes room)
    {
        if (suspect == null || suspect.Data == null)
        {
            return;
        }

        AddSuspicion(suspect.PlayerId, 1.5f);
        LastBodyReporterNote = $"I saw {GameHelpers.ColorName(suspect)} near a body in {GameHelpers.RoomName(room)}";
        _sightings.Add(new Sighting
        {
            PlayerId = suspect.PlayerId,
            PlayerName = suspect.Data.PlayerName,
            Color = GameHelpers.ColorName(suspect),
            Room = room,
            Position = suspect.GetTruePosition(),
            GameTime = Time.time,
            WasNearBody = true,
        });
    }

    public void AddSuspicion(byte playerId, float amount)
    {
        _suspicion.TryGetValue(playerId, out var current);
        _suspicion[playerId] = current + amount;
    }

    public void ReactToAccusation(byte accusedId, byte accuserId, bool againstSelf)
    {
        if (againstSelf)
        {
            AddSuspicion(accuserId, 0.8f);
        }
        else
        {
            AddSuspicion(accusedId, 0.35f);
        }
    }

    public byte? MostSuspicious(IEnumerable<byte> livingIds, byte selfId)
    {
        byte? best = null;
        var bestScore = 0.4f;
        foreach (var id in livingIds)
        {
            if (id == selfId)
            {
                continue;
            }

            if (_suspicion.TryGetValue(id, out var score) && score > bestScore)
            {
                bestScore = score;
                best = id;
            }
        }

        return best;
    }

    public string SummarizeForPrompt(int max = 8)
    {
        if (_sightings.Count == 0)
        {
            return "No notable sightings.";
        }

        return string.Join(
            "\n",
            _sightings
                .TakeLast(max)
                .Select(s =>
                {
                    var flags = s.WasNearBody ? " near body" : s.WasInVent ? " in vent" : "";
                    return $"- Saw {s.Color} ({s.PlayerName}) in {GameHelpers.RoomName(s.Room)}{flags}";
                }));
    }

    public Sighting? LatestInteresting()
    {
        return _sightings.LastOrDefault(s => s.WasNearBody || s.WasInVent) ?? _sightings.LastOrDefault();
    }
}
