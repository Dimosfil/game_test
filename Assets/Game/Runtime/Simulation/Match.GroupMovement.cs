using System;
using System.Collections.Generic;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        /// <summary>Assign loose, separated endpoints using public terrain only. Input order cannot change assignment.
        /// An empty result for a group means that this map cannot provide enough valid formation slots.</summary>
        internal Dictionary<int, SimPoint> GroupDestinations(List<EntityState> actors, SimPoint target)
        {
            var result = new Dictionary<int, SimPoint>();
            if (actors.Count == 0) return result;
            if (actors.Count == 1) { result.Add(actors[0].Id, target); return result; }
            var sorted = new List<EntityState>(actors);
            sorted.Sort((a, b) => a.Id.CompareTo(b.Id));
            int radius = 0, step = 0; long centerX = 0, centerZ = 0;
            foreach (var actor in sorted)
            {
                var definition = Def(actor);
                radius = Math.Max(radius, definition.Radius);
                step = Math.Max(step, definition.Speed / rules.TickRate * (actor.StimRemaining > 0 ? 3 : 2) / 2);
                centerX += actor.Position.X; centerZ += actor.Position.Z;
            }
            // Leave a traversable lane between settled neighbors, plus two arrival movement budgets.
            long spacingValue = 4L * radius + 100 + 2L * step;
            if (spacingValue > int.MaxValue) return result;
            int spacing = (int)spacingValue;
            centerX /= sorted.Count; centerZ /= sorted.Count;
            var anchor = new SimPoint(Math.Max(radius, Math.Min(target.X, rules.MapWidth * 1000 - radius - 1)),
                Math.Max(radius, Math.Min(target.Z, rules.MapHeight * 1000 - radius - 1)));
            var slots = new List<SimPoint>();
            int maxRing = Math.Max(rules.MapWidth, rules.MapHeight) * 1000 / spacing + 2;
            for (int ring = 0; ring <= maxRing && slots.Count < sorted.Count; ring++)
            {
                for (int z = -ring; z <= ring; z++) for (int x = -ring; x <= ring; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) != ring) continue;
                    var slot = new SimPoint(anchor.X + x * spacing, anchor.Z + z * spacing);
                    if (Inside(slot, radius) && !BlockedTerrain(slot, radius)) slots.Add(slot);
                }
            }
            if (slots.Count < sorted.Count) return result;
            slots.Sort((a, b) =>
            {
                int byDistance = DistanceSquared(a, anchor).CompareTo(DistanceSquared(b, anchor));
                return byDistance != 0 ? byDistance : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
            });
            if (slots.Count > sorted.Count) slots.RemoveRange(sorted.Count, slots.Count - sorted.Count);
            foreach (var actor in sorted)
            {
                var desired = new SimPoint((int)(target.X + actor.Position.X - centerX), (int)(target.Z + actor.Position.Z - centerZ));
                int best = 0; long bestDistance = long.MaxValue;
                for (int i = 0; i < slots.Count; i++)
                {
                    long distance = DistanceSquared(slots[i], desired);
                    if (distance < bestDistance) { bestDistance = distance; best = i; }
                }
                result.Add(actor.Id, slots[best]); slots.RemoveAt(best);
            }
            return result;
        }
    }
}
