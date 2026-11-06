using AiCrewmates.AI;
using AiCrewmates.Util;
using Il2CppInterop.Runtime.Attributes;
using Reactor.Utilities.Attributes;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AiCrewmates.Bots;

public enum BotActivity
{
    Idle,
    Walking,
    DoingTask,
    FixingSabotage,
    Hunting,
    Venting,
    Meeting,
}

[RegisterInIl2Cpp]
public sealed class BotBrain : MonoBehaviour
{
    public BotBrain(IntPtr ptr) : base(ptr)
    {
    }

    public PlayerControl Player = null!;
    public BotMemory Memory { get; } = new();
    public BotActivity Activity { get; private set; } = BotActivity.Idle;

    private readonly List<Vector2> _path = new();
    private int _pathIndex;
    private float _stateTimer;
    private float _taskTimer;
    private float _nextDecision;
    private float _nextSabotage;
    private float _ventUntil;
    private PlayerTask? _currentTask;
    private bool _meetingHandled;
    private bool _chatSent;
    private bool _voteSent;
    private Vector2 _wanderTarget;

    public void Init(PlayerControl player)
    {
        Player = player;
        Memory.ClearRound();
        Activity = BotActivity.Idle;
        _nextDecision = Time.time + Random.Range(0.2f, ModConfig.ReactionTimeSeconds.Value);
    }

    private void FixedUpdate()
    {
        if (Player == null || Player.Data == null)
        {
            return;
        }

        if (MeetingHud.Instance != null)
        {
            HandleMeeting();
            return;
        }

        _meetingHandled = false;
        _chatSent = false;
        _voteSent = false;

        if (!GameHelpers.CanActInRound(Player))
        {
            return;
        }

        Memory.Observe(Player, GameHelpers.LivingPlayers(), visionRange: 5.5f);

        if (TryReportBody())
        {
            return;
        }

        if (Time.time < _nextDecision)
        {
            FollowPath();
            return;
        }

        _nextDecision = Time.time + Mathf.Max(0.15f, ModConfig.ReactionTimeSeconds.Value * Random.Range(0.35f, 0.8f));

        if (TryHandleSabotage())
        {
            return;
        }

        if (GameHelpers.IsImpostor(Player))
        {
            TickImpostor();
        }
        else
        {
            TickCrewmate();
        }

        FollowPath();
    }

    private void TickCrewmate()
    {
        if (Activity is BotActivity.DoingTask && _currentTask != null && !_currentTask.IsComplete)
        {
            _taskTimer -= Time.fixedDeltaTime;
            if (_taskTimer <= 0f)
            {
                CompleteCurrentTask();
            }

            return;
        }

        var task = FindNextTask();
        if (task == null)
        {
            Activity = BotActivity.Idle;
            Wander();
            return;
        }

        _currentTask = task;
        var target = TaskPosition(task);
        if (Vector2.Distance(Player.GetTruePosition(), target) <= 0.85f)
        {
            Activity = BotActivity.DoingTask;
            _taskTimer = ModConfig.TaskCompleteDelaySeconds.Value * Random.Range(0.75f, 1.25f);
            return;
        }

        Activity = BotActivity.Walking;
        SetPath(target);
    }

    private void TickImpostor()
    {
        // Occasional sabotage.
        if (Time.time >= _nextSabotage && Random.value < 0.08f)
        {
            TrySabotage();
            _nextSabotage = Time.time + Random.Range(18f, 40f);
        }

        // Kill when ready and target is isolated.
        if (Player.killTimer <= 0.05f && Random.value < ModConfig.KillAggressiveness.Value)
        {
            var victim = FindIsolatedKillTarget();
            if (victim != null)
            {
                Activity = BotActivity.Hunting;
                if (Vector2.Distance(Player.GetTruePosition(), victim.GetTruePosition()) <= GameHelpers.KillRange())
                {
                    try
                    {
                        Player.RpcMurderPlayer(victim, true);
                    }
                    catch
                    {
                        try { Player.CmdCheckMurder(victim); } catch { /* untested API surface */ }
                    }

                    _nextDecision = Time.time + ModConfig.ReactionTimeSeconds.Value;
                    return;
                }

                SetPath(victim.GetTruePosition());
                return;
            }
        }

        // Sometimes vent.
        if (!Player.inVent && Random.value < 0.04f && ShipStatus.Instance?.AllVents != null)
        {
            var vent = ShipStatus.Instance.AllVents[Random.Range(0, ShipStatus.Instance.AllVents.Count)];
            if (vent != null && Vector2.Distance(Player.GetTruePosition(), vent.transform.position) < 1.2f)
            {
                try
                {
                    Player.MyPhysics.RpcEnterVent(vent.Id);
                    Activity = BotActivity.Venting;
                    _ventUntil = Time.time + Random.Range(1.5f, 4f);
                    return;
                }
                catch
                {
                    // Vent RPCs can differ by map/build.
                }
            }
            else if (vent != null)
            {
                SetPath(vent.transform.position);
                Activity = BotActivity.Walking;
                return;
            }
        }

        if (Player.inVent && Time.time >= _ventUntil)
        {
            try
            {
                var vents = ShipStatus.Instance?.AllVents;
                if (vents != null && vents.Count > 0)
                {
                    var exit = vents[Random.Range(0, vents.Count)];
                    Player.MyPhysics.RpcExitVent(exit.Id);
                }
            }
            catch
            {
                // ignore
            }
        }

        // Fake tasks: walk between console landmarks.
        if (_path.Count == 0 || _pathIndex >= _path.Count)
        {
            FakeTaskWander();
        }

        Activity = BotActivity.Walking;
    }

    private bool TryHandleSabotage()
    {
        if (GameHelpers.IsImpostor(Player))
        {
            return false;
        }

        var critical = FindCriticalSabotageTarget();
        if (critical == null)
        {
            return false;
        }

        Activity = BotActivity.FixingSabotage;
        if (Vector2.Distance(Player.GetTruePosition(), critical.Value) <= 1.0f)
        {
            // Host-side repair: poke common sabotage systems toward a repaired state.
            try
            {
                ShipStatus.Instance?.RpcUpdateSystem(SystemTypes.Reactor, 16);
                ShipStatus.Instance?.RpcUpdateSystem(SystemTypes.LifeSupp, 16);
                ShipStatus.Instance?.RpcUpdateSystem(SystemTypes.Electrical, 64);
                ShipStatus.Instance?.RpcUpdateSystem(SystemTypes.Comms, 16);
            }
            catch
            {
                // Repair amounts are map-specific; best-effort.
            }

            _nextDecision = Time.time + 1.5f;
            return true;
        }

        SetPath(critical.Value);
        FollowPath();
        return true;
    }

    private void TrySabotage()
    {
        try
        {
            var options = new[] { SystemTypes.Reactor, SystemTypes.LifeSupp, SystemTypes.Comms, SystemTypes.Electrical };
            var pick = options[Random.Range(0, options.Length)];
            // amount 128 commonly starts a sabotage on SabotageSystemType.
            ShipStatus.Instance?.RpcUpdateSystem(pick, 128);
        }
        catch
        {
            // Best-effort; sabotage encoding varies.
        }
    }

    private Vector2? FindCriticalSabotageTarget()
    {
        // Prefer task list sabotage entries (reactor/o2/lights/comms) assigned to the bot.
        if (Player.myTasks != null)
        {
            foreach (var task in Player.myTasks)
            {
                if (task == null || task.IsComplete)
                {
                    continue;
                }

                if (task.TaskType is TaskTypes.ResetReactor or TaskTypes.ResetSeismic or TaskTypes.RestoreOxy
                    or TaskTypes.FixLights or TaskTypes.FixComms)
                {
                    return TaskPosition(task);
                }
            }
        }

        return null;
    }

    private Vector2? RoomCenter(SystemTypes room)
    {
        var ship = ShipStatus.Instance;
        if (ship?.FastRooms != null && ship.FastRooms.ContainsKey(room))
        {
            var r = ship.FastRooms[room];
            if (r != null)
            {
                return r.transform.position;
            }
        }

        return null;
    }

    private Vector2? LandmarkNear(SystemTypes room)
    {
        var ship = ShipStatus.Instance;
        if (ship?.AllConsoles == null)
        {
            return null;
        }

        foreach (var c in ship.AllConsoles)
        {
            if (c != null && c.Room == room)
            {
                return c.transform.position;
            }
        }

        return null;
    }

    private bool TryReportBody()
    {
        foreach (var body in GameHelpers.FindBodies())
        {
            if (body == null || body.Reported)
            {
                continue;
            }

            var pos = (Vector2)body.transform.position;
            if (Vector2.Distance(Player.GetTruePosition(), pos) > 2.2f)
            {
                continue;
            }

            // Note nearby players as suspicious.
            foreach (var other in GameHelpers.LivingPlayers())
            {
                if (other.PlayerId == Player.PlayerId)
                {
                    continue;
                }

                if (Vector2.Distance(other.GetTruePosition(), pos) < 3.5f)
                {
                    Memory.NoteBodyNear(other, GameHelpers.GuessRoom(pos));
                }
            }

            try
            {
                var info = GameData.Instance?.GetPlayerById(body.ParentId);
                Player.CmdReportDeadBody(info);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Walk toward a visible body if reasonably close.
        foreach (var body in GameHelpers.FindBodies())
        {
            if (body == null || body.Reported)
            {
                continue;
            }

            var pos = (Vector2)body.transform.position;
            if (Vector2.Distance(Player.GetTruePosition(), pos) < 8f)
            {
                SetPath(pos);
                FollowPath();
                return true;
            }
        }

        return false;
    }

    private PlayerControl? FindIsolatedKillTarget()
    {
        PlayerControl? best = null;
        var bestScore = float.MaxValue;
        var myPos = Player.GetTruePosition();
        var range = GameHelpers.KillRange() * 2.5f;

        foreach (var other in GameHelpers.LivingPlayers())
        {
            if (other.PlayerId == Player.PlayerId || GameHelpers.IsImpostor(other))
            {
                continue;
            }

            var dist = Vector2.Distance(myPos, other.GetTruePosition());
            if (dist > range)
            {
                continue;
            }

            var witnesses = GameHelpers.LivingPlayers().Count(p =>
                p.PlayerId != Player.PlayerId &&
                p.PlayerId != other.PlayerId &&
                !GameHelpers.IsImpostor(p) &&
                Vector2.Distance(p.GetTruePosition(), other.GetTruePosition()) < 4.5f);

            var score = dist + witnesses * 4f;
            if (score < bestScore && witnesses == 0)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }

    private PlayerTask? FindNextTask()
    {
        if (Player.myTasks == null)
        {
            return null;
        }

        foreach (var task in Player.myTasks)
        {
            if (task != null && !task.IsComplete && task.TaskType != TaskTypes.FixComms && task.TaskType != TaskTypes.ResetReactor && task.TaskType != TaskTypes.ResetSeismic && task.TaskType != TaskTypes.RestoreOxy && task.TaskType != TaskTypes.FixLights)
            {
                return task;
            }
        }

        return null;
    }

    private Vector2 TaskPosition(PlayerTask task)
    {
        try
        {
            var consoles = task.FindConsoles();
            if (consoles != null && consoles.Count > 0 && consoles[0] != null)
            {
                return consoles[0].transform.position;
            }
        }
        catch
        {
            // Fall through.
        }

        try
        {
            var locs = task.Locations;
            if (locs != null && locs.Count > 0)
            {
                return locs[0];
            }
        }
        catch
        {
            // Fall through.
        }

        return RoomCenter(task.StartAt) ?? Player.GetTruePosition();
    }

    private void CompleteCurrentTask()
    {
        if (_currentTask == null)
        {
            Activity = BotActivity.Idle;
            return;
        }

        try
        {
            _currentTask.Complete();
            Player.RpcCompleteTask(_currentTask.Id);
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogWarning($"Task complete failed: {ex.Message}");
        }

        _currentTask = null;
        Activity = BotActivity.Idle;
    }

    private void FakeTaskWander()
    {
        var ship = ShipStatus.Instance;
        if (ship?.AllConsoles == null || ship.AllConsoles.Count == 0)
        {
            Wander();
            return;
        }

        var console = ship.AllConsoles[Random.Range(0, ship.AllConsoles.Count)];
        if (console != null)
        {
            SetPath(console.transform.position);
        }
    }

    private void Wander()
    {
        if (Vector2.Distance(Player.GetTruePosition(), _wanderTarget) < 0.7f || _path.Count == 0)
        {
            var ship = ShipStatus.Instance;
            if (ship?.AllRooms != null && ship.AllRooms.Count > 0)
            {
                var room = ship.AllRooms[Random.Range(0, ship.AllRooms.Count)];
                _wanderTarget = room != null ? (Vector2)room.transform.position : Player.GetTruePosition();
            }
            else
            {
                _wanderTarget = Player.GetTruePosition() + Random.insideUnitCircle * 4f;
            }

            SetPath(_wanderTarget);
        }
    }

    private void SetPath(Vector2 destination)
    {
        _path.Clear();
        _path.AddRange(Pathfinding.FindPath(Player.GetTruePosition(), destination));
        _pathIndex = 0;
    }

    private void FollowPath()
    {
        if (_path.Count == 0 || _pathIndex >= _path.Count || Player == null)
        {
            return;
        }

        var target = _path[_pathIndex];
        var pos = Player.GetTruePosition();
        var delta = target - pos;
        var dist = delta.magnitude;
        if (dist < 0.35f)
        {
            _pathIndex++;
            return;
        }

        var speed = Player.MyPhysics != null ? Player.MyPhysics.TrueSpeed : 2.5f;
        // FixedUpdate ~50Hz; step toward target.
        var step = Mathf.Min(speed * Time.fixedDeltaTime, dist);
        var next = pos + delta.normalized * step;
        try
        {
            Player.NetTransform.SnapTo(next);
        }
        catch
        {
            Player.transform.position = new Vector3(next.x, next.y, Player.transform.position.z);
        }
    }

    private void HandleMeeting()
    {
        Activity = BotActivity.Meeting;
        if (Player.Data.IsDead || _meetingHandled)
        {
            return;
        }

        // Wait for discussion UI.
        if (MeetingHud.Instance == null)
        {
            return;
        }

        var delay = ModConfig.ReactionTimeSeconds.Value * Random.Range(1.2f, 3.5f);
        if (!_chatSent && Time.timeSinceLevelLoad > 0f)
        {
            // Use state timer to stagger bots.
            if (_stateTimer <= 0f)
            {
                _stateTimer = Time.time + delay;
            }

            if (Time.time >= _stateTimer)
            {
                _ = SpeakAndVoteAsync();
                _chatSent = true;
                _meetingHandled = true;
            }
        }
    }

    [HideFromIl2Cpp]
    private async Task SpeakAndVoteAsync()
    {
        try
        {
            var living = GameHelpers.LivingPlayers()
                .Select(p => (p.PlayerId, p.Data?.PlayerName ?? "?", GameHelpers.ColorName(p)))
                .ToList();

            OpenAiMeetingDecision? ai = null;
            if (ModConfig.HasLiveAi)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                ai = await OpenAiChatClient.DecideAsync(
                    Player.Data?.PlayerName ?? "Bot",
                    GameHelpers.ColorName(Player),
                    GameHelpers.IsImpostor(Player),
                    Memory,
                    living,
                    cts.Token);
            }

            var chat = ai?.Chat;
            if (string.IsNullOrWhiteSpace(chat))
            {
                chat = MeetingBrain.BuildRuleChat(Memory, GameHelpers.IsImpostor(Player), GameHelpers.ColorName(Player));
            }

            if (!string.IsNullOrWhiteSpace(chat) && ModConfig.ChatEnabled.Value)
            {
                try { Player.RpcSendChat(chat); } catch { /* chat may be disabled */ }
            }

            await Task.Delay(TimeSpan.FromSeconds(ModConfig.ReactionTimeSeconds.Value * Random.Range(1.5f, 3f)));

            if (_voteSent || MeetingHud.Instance == null || Player?.Data == null || Player.Data.IsDead)
            {
                return;
            }

            byte vote;
            if (ai != null)
            {
                vote = ai.Skip || ai.VotePlayerId == null ? (byte)253 : ai.VotePlayerId.Value;
            }
            else
            {
                vote = MeetingBrain.DecideRuleVote(Memory, Player);
            }

            // Prefer DummyBehaviour override when present; also cast directly.
            var dummy = Player.GetComponent<DummyBehaviour>();
            if (dummy != null)
            {
                dummy.PlayerIdToVoteFor = vote == 253 ? -1 : vote;
            }

            try
            {
                MeetingHud.Instance.CmdCastVote(Player.PlayerId, vote);
            }
            catch (Exception ex)
            {
                AiCrewmatesPlugin.Instance.Log.LogWarning($"Vote failed for {Player.Data?.PlayerName}: {ex.Message}");
            }

            _voteSent = true;
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogWarning($"Meeting AI error: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        BotManager.NotifyDestroyed(this);
    }
}
