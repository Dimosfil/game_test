using System;
using System.Collections.Generic;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        readonly Dictionary<int, bool[]> walkabilityByRadius = new Dictionary<int, bool[]>();
        PathWorkspace pathWorkspace;
        // Match is exclusively owned and FindPath is synchronous/nonrecursive. Scratch never
        // escapes into a returned route or snapshot; each search resets all traversal state.
        sealed class PathWorkspace
        {
            public readonly int[] Parent, Cost, Offsets;
            public readonly bool[] Closed;
            public readonly PathHeap Open = new PathHeap();
            public PathWorkspace(int width, int height)
            {
                Parent = new int[width * height]; Cost = new int[Parent.Length]; Closed = new bool[Parent.Length];
                Offsets = new[] { -width, -1, 1, width };
            }
            public void Reset()
            {
                Array.Fill(Parent, -1); Array.Fill(Cost, int.MaxValue); Array.Clear(Closed, 0, Closed.Length);
                Open.Clear();
            }
        }
        readonly struct StaticCircle
        {
            public readonly EntityState Entity;
            public readonly int Radius;
            public StaticCircle(EntityState entity, int radius) { Entity = entity; Radius = radius; }
        }
        readonly List<StaticCircle> staticObstacles = new List<StaticCircle>();
        const int StaticBucketSize = 8000;
        readonly Dictionary<long, List<StaticCircle>> staticBuckets = new Dictionary<long, List<StaticCircle>>();
        int largestStaticRadius;
        bool staticObstaclesReady;
        const int UnitBucketSize = 2000;
        readonly Dictionary<long, List<EntityState>> unitBuckets = new Dictionary<long, List<EntityState>>();
        int unitIndexTick = -1, unitIndexAllocator = -1, largestUnitRadius;
        void InvalidateNavigation() { walkabilityByRadius.Clear(); staticObstaclesReady = false; }
        void EnsureStaticObstacles()
        {
            if (staticObstaclesReady) return;
            staticObstacles.Clear();
            foreach (var bucket in staticBuckets.Values) bucket.Clear();
            largestStaticRadius = 0;
            foreach (var e in state.Entities)
            {
                if (e.Health <= 0) continue;
                var definition = Def(e);
                if (!definition.IsBuilding && !definition.IsResource) continue;
                var circle = new StaticCircle(e, definition.Radius);
                staticObstacles.Add(circle);
                largestStaticRadius = Math.Max(largestStaticRadius, circle.Radius);
                long key = BucketKey(e.Position.X / StaticBucketSize, e.Position.Z / StaticBucketSize);
                if (!staticBuckets.TryGetValue(key, out var bucket)) { bucket = new List<StaticCircle>(); staticBuckets.Add(key, bucket); }
                bucket.Add(circle);
            }
            staticObstaclesReady = true;
        }
        static long BucketKey(int x, int z) => ((long)x << 32) | (uint)z;
        static long BucketKey(SimPoint p) => BucketKey(p.X / UnitBucketSize, p.Z / UnitBucketSize);
        void AddToUnitBucket(EntityState e)
        {
            long key = BucketKey(e.Position);
            if (!unitBuckets.TryGetValue(key, out var bucket)) { bucket = new List<EntityState>(); unitBuckets.Add(key, bucket); }
            bucket.Add(e);
        }
        void EnsureUnitIndex()
        {
            if (unitIndexTick == state.Tick && unitIndexAllocator == state.NextEntityId) return;
            foreach (var bucket in unitBuckets.Values) bucket.Clear();
            largestUnitRadius = 0;
            foreach (var e in state.Entities)
            {
                if (e.Health <= 0) continue;
                var d = Def(e);
                if (d.IsBuilding || d.IsResource) continue;
                largestUnitRadius = Math.Max(largestUnitRadius, d.Radius);
                AddToUnitBucket(e);
            }
            unitIndexTick = state.Tick; unitIndexAllocator = state.NextEntityId;
        }
        void MoveEntity(EntityState e, SimPoint position)
        {
            if (DetailedDiagnosticsEnabled) RecordMarineMovement(e, position);
            EnsureUnitIndex();
            long before = BucketKey(e.Position), after = BucketKey(position);
            if (before != after && unitBuckets.TryGetValue(before, out var bucket)) bucket.Remove(e);
            e.Position = position;
            if (before != after) AddToUnitBucket(e);
        }
        bool[] WalkabilityGrid(int radius)
        {
            if (walkabilityByRadius.TryGetValue(radius, out var cached)) return cached;
            var grid = new bool[rules.MapWidth * rules.MapHeight];
            for (int i = 0; i < grid.Length; i++) grid[i] = Walkable(new SimPoint(i % rules.MapWidth * 1000 + 500, i / rules.MapWidth * 1000 + 500), radius);
            walkabilityByRadius.Add(radius, grid);
            return grid;
        }
        static int Root(long value)
        {
            if (value <= 0) return 0;
            long x = value, y = (x + 1) / 2;
            while (y < x) { x = y; y = (x + value / x) / 2; }
            return (int)x;
        }
        void ClearPath(EntityState e) { e.Path.Clear(); e.PathIndex = 0; e.PathTarget = default; e.PathStoppingDistance = 0; }
        bool BlockedTerrain(SimPoint p, int radius)
        {
            foreach (var b in rules.Blocks)
            {
                int minX = b.X * 1000, minZ = b.Z * 1000;
                int x = Math.Max(minX, Math.Min(p.X, minX + b.Width * 1000));
                int z = Math.Max(minZ, Math.Min(p.Z, minZ + b.Height * 1000));
                if (DistanceSquared(p, new SimPoint(x, z)) < (long)radius * radius ||
                    (p.X >= minX && p.X < minX + b.Width * 1000 && p.Z >= minZ && p.Z < minZ + b.Height * 1000)) return true;
            }
            return false;
        }
        bool Walkable(SimPoint p, int radius, int ignoreId = 0)
        {
            if (!Inside(p, radius) || BlockedTerrain(p, radius)) return false;
            EnsureStaticObstacles();
            foreach (var obstacle in staticObstacles)
            {
                if (obstacle.Entity.Id == ignoreId || obstacle.Entity.Health <= 0) continue;
                long combined = radius + obstacle.Radius;
                if (DistanceSquared(p, obstacle.Entity.Position) < combined * combined) return false;
            }
            return true;
        }
        bool UnitsClear(EntityState e, SimPoint p)
        {
            EnsureUnitIndex();
            int ownRadius = Def(e).Radius, searchRadius = ownRadius + largestUnitRadius;
            int minX = (p.X - searchRadius) / UnitBucketSize, maxX = (p.X + searchRadius) / UnitBucketSize;
            int minZ = (p.Z - searchRadius) / UnitBucketSize, maxZ = (p.Z + searchRadius) / UnitBucketSize;
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                if (!unitBuckets.TryGetValue(BucketKey(x, z), out var bucket)) continue;
                foreach (var other in bucket)
                {
                    if (other.Id == e.Id || other.Health <= 0) continue;
                    int radius = Def(other).Radius + ownRadius;
                    if (DistanceSquared(p, other.Position) < (long)radius * radius) return false;
                }
            }
            return true;
        }
        bool ClearSegment(EntityState e, SimPoint from, SimPoint to, bool avoidUnits)
        {
            if (!StaticSegmentClear(e, from, to)) return false;
            return !avoidUnits || UnitSegmentClear(e, from, to);
        }
        bool UnitSegmentClear(EntityState e, SimPoint from, SimPoint to)
        {
            EnsureUnitIndex();
            int ownRadius = Def(e).Radius, searchRadius = ownRadius + largestUnitRadius;
            int minX = (Math.Min(from.X, to.X) - searchRadius) / UnitBucketSize, maxX = (Math.Max(from.X, to.X) + searchRadius) / UnitBucketSize;
            int minZ = (Math.Min(from.Z, to.Z) - searchRadius) / UnitBucketSize, maxZ = (Math.Max(from.Z, to.Z) + searchRadius) / UnitBucketSize;
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                if (!unitBuckets.TryGetValue(BucketKey(x, z), out var bucket)) continue;
                foreach (var other in bucket)
                    if (other.Id != e.Id && other.Health > 0 && SegmentTouchesCircle(from, to, other.Position, ownRadius + Def(other).Radius)) return false;
            }
            return true;
        }
        static bool SegmentTouchesCircle(SimPoint from, SimPoint to, SimPoint center, int radius)
        {
            // A strict circle interior cannot intersect a segment whose bounds miss its open box.
            // Use long absolute extent so the existing squared-radius behavior also survives int overflow.
            long extent = Math.Abs((long)radius);
            if ((long)Math.Max(from.X, to.X) <= center.X - extent || (long)Math.Min(from.X, to.X) >= center.X + extent ||
                (long)Math.Max(from.Z, to.Z) <= center.Z - extent || (long)Math.Min(from.Z, to.Z) >= center.Z + extent) return false;
            long dx = (long)to.X - from.X, dz = (long)to.Z - from.Z;
            long cx = (long)center.X - from.X, cz = (long)center.Z - from.Z;
            long length = dx * dx + dz * dz, projection = cx * dx + cz * dz;
            if (length == 0 || projection <= 0) return DistanceSquared(from, center) < (long)radius * radius;
            if (projection >= length) return DistanceSquared(to, center) < (long)radius * radius;
            // Coordinates remain integers. Decimal holds the exact squared cross product beyond Int64 range.
            decimal cross = (decimal)cx * dz - (decimal)cz * dx;
            return cross * cross < (decimal)radius * radius * length;
        }
        static bool ClipOpenAxis(int origin, int delta, int min, int max, ref decimal entry, ref decimal exit)
        {
            if (delta == 0) return origin > min && origin < max;
            decimal a = ((decimal)min - origin) / delta, b = ((decimal)max - origin) / delta;
            if (a > b) { decimal swap = a; a = b; b = swap; }
            entry = Math.Max(entry, a); exit = Math.Min(exit, b);
            return entry < exit;
        }
        static bool SegmentTouchesOpenBox(SimPoint from, SimPoint to, int minX, int minZ, int maxX, int maxZ)
        {
            decimal entry = 0, exit = 1;
            return ClipOpenAxis(from.X, to.X - from.X, minX, maxX, ref entry, ref exit) &&
                ClipOpenAxis(from.Z, to.Z - from.Z, minZ, maxZ, ref entry, ref exit) && entry < exit;
        }
        bool StaticSegmentClear(EntityState e, SimPoint from, SimPoint to)
        {
            int radius = Def(e).Radius;
            if (!Inside(from, radius) || !Inside(to, radius)) return false;
            foreach (var block in rules.Blocks)
            {
                int x0 = block.X * 1000, z0 = block.Z * 1000;
                int x1 = (block.X + block.Width) * 1000, z1 = (block.Z + block.Height) * 1000;
                if (SegmentTouchesOpenBox(from, to, x0 - radius, z0, x1 + radius, z1) ||
                    SegmentTouchesOpenBox(from, to, x0, z0 - radius, x1, z1 + radius) ||
                    SegmentTouchesCircle(from, to, new SimPoint(x0, z0), radius) ||
                    SegmentTouchesCircle(from, to, new SimPoint(x0, z1), radius) ||
                    SegmentTouchesCircle(from, to, new SimPoint(x1, z0), radius) ||
                    SegmentTouchesCircle(from, to, new SimPoint(x1, z1), radius)) return false;
            }
            EnsureStaticObstacles();
            int searchRadius = radius + largestStaticRadius;
            int minX = Math.Max(0, (Math.Min(from.X, to.X) - searchRadius) / StaticBucketSize);
            int maxX = Math.Min((rules.MapWidth * 1000 - 1) / StaticBucketSize, (Math.Max(from.X, to.X) + searchRadius) / StaticBucketSize);
            int minZ = Math.Max(0, (Math.Min(from.Z, to.Z) - searchRadius) / StaticBucketSize);
            int maxZ = Math.Min((rules.MapHeight * 1000 - 1) / StaticBucketSize, (Math.Max(from.Z, to.Z) + searchRadius) / StaticBucketSize);
            // A long route or an unusually large radius can cover more empty buckets than obstacles.
            if ((long)(maxX - minX + 1) * (maxZ - minZ + 1) >= staticObstacles.Count)
            {
                foreach (var obstacle in staticObstacles)
                    if (StaticCircleBlocks(obstacle, e.Id, radius, from, to)) return false;
            }
            else for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                if (!staticBuckets.TryGetValue(BucketKey(x, z), out var bucket)) continue;
                foreach (var obstacle in bucket)
                    if (StaticCircleBlocks(obstacle, e.Id, radius, from, to)) return false;
            }
            return true;
        }
        static bool StaticCircleBlocks(StaticCircle obstacle, int moverId, int radius, SimPoint from, SimPoint to) =>
            obstacle.Entity.Health > 0 && obstacle.Entity.Id != moverId &&
            SegmentTouchesCircle(from, to, obstacle.Entity.Position, radius + obstacle.Radius);
        bool MoveToward(EntityState e, SimPoint destination, int stoppingDistance = 0)
        {
            if (e.PathTarget.X != destination.X || e.PathTarget.Z != destination.Z || e.PathStoppingDistance != stoppingDistance) ClearPath(e);
            if (Near(e.Position, destination, stoppingDistance)) { ClearPath(e); return true; }
            int speed = Math.Max(1, Def(e).Speed / rules.TickRate);
            if (e.StimRemaining > 0) speed = speed * 3 / 2;
            if (e.PathIndex >= e.Path.Count)
            {
                e.Path = FindPath(e, destination, stoppingDistance);
                e.PathIndex = 0;
                e.PathTarget = destination; e.PathStoppingDistance = stoppingDistance;
                if (e.Path.Count == 0) return false;
            }
            SimPoint waypoint = e.Path[e.PathIndex];
            if (stoppingDistance == 0 && e.PathIndex + 1 < e.Path.Count && !UnitsClear(e, waypoint))
                waypoint = SkipOccupiedWaypoint(e);
            // Near an endpoint, cell-centre waypoints needlessly funnel distinct formation slots together.
            // Keep the authored path for diagnostics, but steer directly when the terminal static segment is clear.
            bool terminalSteering = stoppingDistance == 0 && Near(e.Position, destination, 5000) && ClearSegment(e, e.Position, destination, false);
            if (terminalSteering) waypoint = destination;
            long dx = (long)waypoint.X - e.Position.X, dz = (long)waypoint.Z - e.Position.Z;
            int distance = Root(dx * dx + dz * dz);
            int amount = Math.Min(distance, speed);
            var next = distance == 0 ? waypoint : new SimPoint(e.Position.X + (int)(dx * amount / distance), e.Position.Z + (int)(dz * amount / distance));
            bool staticBlocked = !ClearSegment(e, e.Position, next, false);
            if (staticBlocked || !UnitSegmentClear(e, e.Position, next))
            {
                // A rounded short step can graze a corner even when the saved longer segment is legal.
                // Repair from a safe steered position; merely replanning at the unchanged start loops forever.
                if (staticBlocked) ClearPath(e);
                // Evaluate a bounded steering fan; deterministic scoring avoids a permanent leftward orbit.
                int vx = distance > 0 ? (int)(dx * speed / distance) : 0;
                int vz = distance > 0 ? (int)(dz * speed / distance) : 0;
                bool moved = false; SimPoint best = e.Position;
                int side = e.AvoidanceSide == 0 ? 1 : e.AvoidanceSide;
                for (int pass = 0; pass < 2 && !moved; pass++)
                {
                    long bestScore = long.MaxValue;
                    for (int option = 0; option < 7; option++)
                    {
                        int x, z;
                        switch (option)
                        {
                            case 0: x = vx / 2; z = vz / 2; break;
                            case 1: x = (vx - vz) * 707 / 1000; z = (vx + vz) * 707 / 1000; break;
                            case 2: x = (vx + vz) * 707 / 1000; z = (vz - vx) * 707 / 1000; break;
                            case 3: x = -vz; z = vx; break;
                            case 4: x = vz; z = -vx; break;
                            case 5: x = (-vx - vz) * 707 / 1000; z = (vx - vz) * 707 / 1000; break;
                            default: x = (vz - vx) * 707 / 1000; z = (-vx - vz) * 707 / 1000; break;
                        }
                        // Persist a hand around the blocker; greedy rescoring both sides creates two-position cycles.
                        if (((long)vx * z - (long)vz * x) * side < 0) continue;
                        var alt = new SimPoint(e.Position.X + x, e.Position.Z + z);
                        long score = DistanceSquared(alt, waypoint);
                        if ((x == 0 && z == 0) || score >= bestScore || !ClearSegment(e, e.Position, alt, true)) continue;
                        best = alt; bestScore = score; moved = true;
                    }
                    if (!moved) side = -side;
                }
                if (moved) { e.AvoidanceSide = side; MoveEntity(e, best); }
                if (!moved && state.Tick % rules.TickRate == e.Id % rules.TickRate) ClearPath(e);
                return false;
            }
            e.AvoidanceSide = 0;
            MoveEntity(e, next);
            if (!terminalSteering && distance <= speed) e.PathIndex++;
            if (Near(e.Position, destination, Math.Max(stoppingDistance, speed)))
            {
                if (stoppingDistance == 0 && ClearSegment(e, e.Position, destination, true)) MoveEntity(e, destination);
                ClearPath(e); return true;
            }
            if (stoppingDistance == 0 && e.PathIndex >= e.Path.Count && !Walkable(destination, Def(e).Radius, e.Id)) { ClearPath(e); return true; }
            return false;
        }
        SimPoint SkipOccupiedWaypoint(EntityState e)
        {
            int first = e.PathIndex, last = Math.Min(e.Path.Count - 1, first + 4);
            for (int candidate = first + 1; candidate <= last; candidate++)
            {
                var point = e.Path[candidate];
                if (!UnitsClear(e, point) || !ClearSegment(e, e.Position, point, false)) continue;
                e.PathIndex = candidate; return point;
            }
            // Whole metre samples can all coincide with a row of settled circles while legal gaps remain.
            // Search the same four segments at quarter intervals, keeping the repair in serialized Path.
            for (int segment = first + 1; segment <= last; segment++)
            {
                var from = e.Path[segment - 1]; var to = e.Path[segment];
                for (int quarter = 1; quarter < 4; quarter++)
                {
                    var point = new SimPoint(from.X + (to.X - from.X) * quarter / 4, from.Z + (to.Z - from.Z) * quarter / 4);
                    if (!UnitsClear(e, point) || !ClearSegment(e, e.Position, point, false)) continue;
                    e.PathIndex = segment - 1; e.Path[e.PathIndex] = point; return point;
                }
            }
            return e.Path[first];
        }

        List<SimPoint> FindPath(EntityState mover, SimPoint destination, int stoppingDistance)
        {
            if (stoppingDistance == 0 && ClearSegment(mover, mover.Position, destination, false))
            {
                var direct = new List<SimPoint>();
                AppendStraightSamples(direct, mover.Position, destination);
                if (PathSegmentsClear(mover, direct)) return direct;
            }
            int w = rules.MapWidth, h = rules.MapHeight, size = w * h;
            int startX = Math.Max(0, Math.Min(w - 1, mover.Position.X / 1000));
            int startZ = Math.Max(0, Math.Min(h - 1, mover.Position.Z / 1000));
            int start = startZ * w + startX;
            // Structures/resources alone determine this grid. Reuse it until their geometry changes.
            // The mover's start is a per-search exception, never written into the shared grid.
            var allowed = WalkabilityGrid(Def(mover).Radius);
            // Cell centres are 500 + 1000*i. Exact midpoint ties choose the lower
            // row-major index, just as the strict-distance scan below always did.
            int nearestX = (int)Math.Max(0L, Math.Min(w - 1L, ((long)destination.X - 1) / 1000));
            int nearestZ = (int)Math.Max(0L, Math.Min(h - 1L, ((long)destination.Z - 1) / 1000));
            int goal = nearestZ * w + nearestX;
            // Outside the validated map domain retain the original scan, including its
            // historical Int64 distance arithmetic for arbitrary restored/internal inputs.
            if (!Inside(destination) || !allowed[goal] && goal != start)
            {
                goal = -1; long closest = long.MaxValue;
                for (int i = 0; i < size; i++)
                {
                    if (!allowed[i] && i != start) continue;
                    long distance = DistanceSquared(new SimPoint(i % w * 1000 + 500, i / w * 1000 + 500), destination);
                    if (distance < closest) { closest = distance; goal = i; }
                }
            }
            if (goal < 0) return new List<SimPoint>();
            var workspace = pathWorkspace ?? (pathWorkspace = new PathWorkspace(w, h));
            workspace.Reset();
            var parent = workspace.Parent; var cost = workspace.Cost; var closed = workspace.Closed;
            cost[start] = 0;
            var open = workspace.Open;
            open.Push(start, Heuristic(start, goal, w));
            int reached = -1;
            var offsets = workspace.Offsets;
            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                closed[current] = true;
                var point = new SimPoint(current % w * 1000 + 500, current / w * 1000 + 500);
                if (current == goal || (stoppingDistance > 0 && Near(point, destination, stoppingDistance))) { reached = current; break; }
                foreach (int offset in offsets)
                {
                    int next = current + offset;
                    if (next < 0 || next >= size || Math.Abs(next % w - current % w) + Math.Abs(next / w - current / w) != 1 || !allowed[next] || closed[next]) continue;
                    var edgeStart = current == start ? mover.Position : point;
                    if (!ClearSegment(mover, edgeStart, new SimPoint(next % w * 1000 + 500, next / w * 1000 + 500), false)) continue;
                    int candidate = cost[current] + 10;
                    if (candidate >= cost[next]) continue;
                    cost[next] = candidate; parent[next] = current;
                    open.Push(next, candidate + Heuristic(next, goal, w));
                }
            }
            var path = new List<SimPoint>();
            if (reached < 0) return path;
            for (int cursor = reached; cursor != start && cursor >= 0; cursor = parent[cursor]) path.Add(new SimPoint(cursor % w * 1000 + 500, cursor / w * 1000 + 500));
            path.Reverse();
            if (stoppingDistance == 0 && Walkable(destination, Def(mover).Radius, mover.Id)) path.Add(destination);
            else
            {
                var end = new SimPoint(reached % w * 1000 + 500, reached / w * 1000 + 500);
                long dx = (long)end.X - destination.X, dz = (long)end.Z - destination.Z;
                int distance = Root(dx * dx + dz * dz);
                if (stoppingDistance > 0 && distance > stoppingDistance && distance > 0)
                {
                    var approach = new SimPoint(destination.X + (int)(dx * stoppingDistance / distance), destination.Z + (int)(dz * stoppingDistance / distance));
                    if (Walkable(approach, Def(mover).Radius, mover.Id)) path.Add(approach);
                }
                if (path.Count == 0) path.Add(end);
            }
            return stoppingDistance == 0 ? SmoothCorridor(mover, path) : path;
        }
        static void AppendStraightSamples(List<SimPoint> output, SimPoint from, SimPoint to)
        {
            long dx = (long)to.X - from.X, dz = (long)to.Z - from.Z;
            int samples = Math.Max(1, (Root(dx * dx + dz * dz) + 999) / 1000);
            for (int i = 1; i <= samples; i++)
                output.Add(new SimPoint(from.X + (int)(dx * i / samples), from.Z + (int)(dz * i / samples)));
        }
        bool PathSegmentsClear(EntityState mover, List<SimPoint> path)
        {
            var from = mover.Position;
            foreach (var point in path)
            {
                if (!ClearSegment(mover, from, point, false)) return false;
                from = point;
            }
            return true;
        }
        List<SimPoint> SmoothCorridor(EntityState mover, List<SimPoint> path)
        {
            if (path.Count < 2) return path;
            var result = new List<SimPoint>(path.Count);
            var from = mover.Position;
            int cursor = 0;
            while (cursor < path.Count)
            {
                int best = cursor, stride = 1, firstBlocked = path.Count;
                // Find the end of the next visible corridor section without a quadratic reverse scan.
                while (cursor + stride < path.Count)
                {
                    int candidate = cursor + stride;
                    if (!ClearSegment(mover, from, path[candidate], false)) { firstBlocked = candidate; break; }
                    best = candidate; stride *= 2;
                }
                int lower = best + 1, upper = Math.Min(path.Count - 1, firstBlocked - 1);
                while (lower <= upper)
                {
                    int middle = lower + (upper - lower) / 2;
                    if (ClearSegment(mover, from, path[middle], false)) { best = middle; lower = middle + 1; }
                    else upper = middle - 1;
                }
                AppendStraightSamples(result, from, path[best]);
                from = path[best]; cursor = best + 1;
            }
            // Integer interpolation can move a grazing segment by <1mm. Check the actual saved polyline,
            // not just its ideal straight anchor, and retain the legal grid corridor when rounding cuts a corner.
            return PathSegmentsClear(mover, result) ? result : path;
        }
        static int Heuristic(int a, int b, int width) => (Math.Abs(a % width - b % width) + Math.Abs(a / width - b / width)) * 10;

        sealed class PathHeap
        {
            struct Entry { public int Node; public int Score; }
            readonly List<Entry> heap = new List<Entry>();
            public int Count => heap.Count;
            public void Clear() => heap.Clear();
            static bool Before(Entry a, Entry b) => a.Score < b.Score || (a.Score == b.Score && a.Node < b.Node);
            public void Push(int node, int score)
            {
                var e = new Entry { Node = node, Score = score }; heap.Add(e); int i = heap.Count - 1;
                while (i > 0) { int p = (i - 1) / 2; if (!Before(e, heap[p])) break; heap[i] = heap[p]; i = p; }
                heap[i] = e;
            }
            public int Pop()
            {
                int result = heap[0].Node; var last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
                if (heap.Count == 0) return result;
                int i = 0;
                while (i * 2 + 1 < heap.Count)
                {
                    int c = i * 2 + 1;
                    if (c + 1 < heap.Count && Before(heap[c + 1], heap[c])) c++;
                    if (!Before(heap[c], last)) break;
                    heap[i] = heap[c]; i = c;
                }
                heap[i] = last; return result;
            }
        }
    }
}
