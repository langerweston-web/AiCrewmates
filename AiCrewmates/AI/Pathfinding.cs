using UnityEngine;

namespace AiCrewmates.AI;

/// <summary>
/// Coarse graph pathfinding over map landmarks (consoles, vents, room centers).
/// Not a full navmesh; good enough for task routing on walkable ship areas.
/// </summary>
public static class Pathfinding
{
    private const float ConnectRadius = 8.5f;
    private static readonly List<Vector2> Nodes = new();
    private static int _cachedMap = -1;

    public static void Invalidate()
    {
        Nodes.Clear();
        _cachedMap = -1;
    }

    public static List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        EnsureGraph();
        if (Nodes.Count == 0)
        {
            return new List<Vector2> { to };
        }

        var start = NearestIndex(from);
        var goal = NearestIndex(to);
        if (start < 0 || goal < 0)
        {
            return new List<Vector2> { to };
        }

        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float> { [start] = 0f };
        var open = new SortedSet<(float f, int i)>(Comparer<(float f, int i)>.Create((a, b) =>
        {
            var c = a.f.CompareTo(b.f);
            return c != 0 ? c : a.i.CompareTo(b.i);
        }));
        open.Add((Heuristic(Nodes[start], Nodes[goal]), start));
        var closed = new HashSet<int>();

        while (open.Count > 0)
        {
            var current = open.Min.i;
            open.Remove(open.Min);
            if (current == goal)
            {
                var path = Reconstruct(cameFrom, current);
                path.Add(to);
                return path;
            }

            closed.Add(current);
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (closed.Contains(i) || i == current)
                {
                    continue;
                }

                var dist = Vector2.Distance(Nodes[current], Nodes[i]);
                if (dist > ConnectRadius || !HasLineOfSight(Nodes[current], Nodes[i]))
                {
                    continue;
                }

                var tentative = gScore[current] + dist;
                if (gScore.TryGetValue(i, out var existing) && tentative >= existing)
                {
                    continue;
                }

                cameFrom[i] = current;
                gScore[i] = tentative;
                open.Add((tentative + Heuristic(Nodes[i], Nodes[goal]), i));
            }
        }

        return new List<Vector2> { to };
    }

    private static void EnsureGraph()
    {
        var ship = ShipStatus.Instance;
        if (ship == null)
        {
            return;
        }

        var mapId = GameOptionsManager.Instance?.CurrentGameOptions?.MapId ?? 0;
        if (_cachedMap == mapId && Nodes.Count > 0)
        {
            return;
        }

        Nodes.Clear();
        _cachedMap = mapId;

        void Add(Vector2 p)
        {
            if (Nodes.Any(n => Vector2.Distance(n, p) < 0.75f))
            {
                return;
            }

            Nodes.Add(p);
        }

        if (ship.AllConsoles != null)
        {
            foreach (var c in ship.AllConsoles)
            {
                if (c != null)
                {
                    Add(c.transform.position);
                }
            }
        }

        if (ship.AllVents != null)
        {
            foreach (var v in ship.AllVents)
            {
                if (v != null)
                {
                    Add(v.transform.position);
                }
            }
        }

        if (ship.AllRooms != null)
        {
            foreach (var room in ship.AllRooms)
            {
                if (room != null)
                {
                    Add(room.transform.position);
                }
            }
        }

        // Seed a loose grid around existing landmarks for better connectivity.
        var snapshot = Nodes.ToList();
        foreach (var n in snapshot)
        {
            Add(n + Vector2.left * 2f);
            Add(n + Vector2.right * 2f);
            Add(n + Vector2.up * 2f);
            Add(n + Vector2.down * 2f);
        }
    }

    private static int NearestIndex(Vector2 p)
    {
        var best = -1;
        var bestD = float.MaxValue;
        for (var i = 0; i < Nodes.Count; i++)
        {
            var d = Vector2.Distance(Nodes[i], p);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    private static float Heuristic(Vector2 a, Vector2 b) => Vector2.Distance(a, b);

    private static List<Vector2> Reconstruct(Dictionary<int, int> cameFrom, int current)
    {
        var path = new List<Vector2> { Nodes[current] };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(Nodes[current]);
        }

        path.Reverse();
        return path;
    }

    private static bool HasLineOfSight(Vector2 a, Vector2 b)
    {
        var delta = b - a;
        var dist = delta.magnitude;
        if (dist < 0.05f)
        {
            return true;
        }

        // Layer mask: default ship walls are usually on Default / Ship layers.
        // If the ray hits something solid before the destination, treat as blocked.
        var hit = Physics2D.Raycast(a, delta.normalized, dist, Constants.ShipOnlyMask);
        return hit.collider == null;
    }
}
