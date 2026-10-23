using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        // Derived masks belong to a stable authoritative state, never to an Observation's mutable arrays.
        readonly bool[][] visibilityMasks = new bool[2][];
        readonly int[] visibilityTicks = { -1, -1 };
        // Private staging under exclusive Match ownership; never publish these live references.
        readonly List<EntityState> observationEntities = new List<EntityState>();
        MatchState visibilityState;
        bool advancingState;
        static readonly HashSet<string> hiddenRouteFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(EntityState.Path), nameof(EntityState.PathIndex), nameof(EntityState.PathTarget),
            nameof(EntityState.PathStoppingDistance), nameof(EntityState.AvoidanceSide)
        };
        static readonly HashSet<string> hiddenOpponentFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(EntityState.Orders), nameof(EntityState.Production), nameof(EntityState.Research),
            nameof(EntityState.Cargo), nameof(EntityState.WorkTimer), nameof(EntityState.GatherTargetId),
            nameof(EntityState.RallyPoint), nameof(EntityState.RallyTargetId)
        };
        static readonly Lazy<FieldInfo[]> ownObservationFields = new Lazy<FieldInfo[]>(() =>
            DataFields(typeof(EntityState)).Where(field => !hiddenRouteFields.Contains(field.Name)).ToArray());
        static readonly Lazy<FieldInfo[]> opponentObservationFields = new Lazy<FieldInfo[]>(() =>
            ownObservationFields.Value.Where(field => !hiddenOpponentFields.Contains(field.Name)).ToArray());
        static readonly Lazy<Func<EntityState, EntityState>> ownObservationProjector =
            new Lazy<Func<EntityState, EntityState>>(() => BuildObservationProjector(ownObservationFields.Value));
        static readonly Lazy<Func<EntityState, EntityState>> opponentObservationProjector =
            new Lazy<Func<EntityState, EntityState>>(() => BuildObservationProjector(opponentObservationFields.Value));

        static Func<EntityState, EntityState> ReflectionObservationProjector(FieldInfo[] selectedFields) => source =>
        {
            var copy = new EntityState();
            CopyFields(source, copy, selectedFields);
            return copy;
        };

        static Func<EntityState, EntityState> BuildObservationProjector(FieldInfo[] selectedFields)
        {
#if ENABLE_IL2CPP
            // IL2CPP cannot emit dynamic methods. Preserve the same policy and detached reference copy.
            return ReflectionObservationProjector(selectedFields);
#else
            var source = Expression.Parameter(typeof(EntityState), "source");
            var target = Expression.Variable(typeof(EntityState), "copy");
            var projection = Expression.Lambda<Func<EntityState, EntityState>>(
                TypedCopyBody(source, target, selectedFields), source);
            try { return projection.Compile(); }
            catch (NotSupportedException)
            {
                // Other AOT runtimes may reject expression compilation; contract/model errors remain visible.
                return ReflectionObservationProjector(selectedFields);
            }
#endif
        }
        bool IsVisible(int player, EntityState entity)
        {
            if (entity.Owner == player) return true;
            foreach (var viewer in state.Entities)
                if (viewer.Owner == player && viewer.Health > 0 && viewer.Completed &&
                    Near(viewer.Position, entity.Position, Def(viewer).Sight)) return true;
            return false;
        }
        bool[] Visibility(int player)
        {
            if (advancingState)
            {
                var transient = new bool[rules.MapWidth * rules.MapHeight];
                FillVisibility(player, transient);
                return transient;
            }
            if (!ReferenceEquals(visibilityState, state))
            {
                visibilityState = state;
                visibilityTicks[0] = visibilityTicks[1] = -1;
            }
            if (visibilityTicks[player] == state.Tick) return visibilityMasks[player];
            var visible = visibilityMasks[player];
            if (visible == null) visibilityMasks[player] = visible = new bool[rules.MapWidth * rules.MapHeight];
            else Array.Clear(visible, 0, visible.Length);
            FillVisibility(player, visible);
            visibilityTicks[player] = state.Tick;
            return visible;
        }
        void FillVisibility(int player, bool[] visible)
        {
            foreach (var e in state.Entities)
            {
                if (e.Owner != player || e.Health <= 0 || !e.Completed) continue;
                int sight = Def(e).Sight;
                // Retain the original clamped/truncated bounding-box semantics, including large int sight.
                int x0 = Math.Max(0, (e.Position.X - sight) / 1000), x1 = Math.Min(rules.MapWidth - 1, (e.Position.X + sight) / 1000);
                int z0 = Math.Max(0, (e.Position.Z - sight) / 1000), z1 = Math.Min(rules.MapHeight - 1, (e.Position.Z + sight) / 1000);
                long radiusSquared = (long)sight * sight;
                for (int z = z0; z <= z1; z++)
                {
                    long dz = z * 1000L + 500 - e.Position.Z;
                    long squared = radiusSquared - dz * dz;
                    if (squared < 0) continue;
                    long extent = (long)Math.Sqrt(squared);
                    // Correct floating-point sqrt rounding before deriving exact inclusive integer endpoints.
                    while (extent * extent > squared) extent--;
                    while ((extent + 1) * (extent + 1) <= squared) extent++;
                    long lastCenter = e.Position.X + extent - 500;
                    if (lastCenter < 0) continue;
                    int first = Math.Max(x0, (int)((Math.Max(0, e.Position.X - extent - 500) + 999) / 1000));
                    int last = Math.Min(x1, (int)(lastCenter / 1000));
                    if (first <= last) Array.Fill(visible, true, z * rules.MapWidth + first, last - first + 1);
                }
            }
        }
        void RefreshKnowledge()
        {
            foreach (var p in state.Players)
            {
                var visible = Visibility(p.Id);
                for (int i = 0; i < visible.Length; i++) p.Explored[i] |= visible[i];
                for (int m = p.RememberedBuildings.Count - 1; m >= 0; m--)
                {
                    var memory = p.RememberedBuildings[m];
                    int index = memory.Position.Z / 1000 * rules.MapWidth + memory.Position.X / 1000;
                    if (index >= 0 && index < visible.Length && visible[index])
                    {
                        bool alive = false;
                        foreach (var e in state.Entities) if (e.Id == memory.Id && e.Health > 0) { alive = true; break; }
                        if (!alive) p.RememberedBuildings.RemoveAt(m);
                    }
                }
                foreach (var e in state.Entities)
                {
                    if (e.Owner < 0 || e.Owner == p.Id || !Def(e).IsBuilding || e.Health <= 0 || !IsVisible(p.Id, e)) continue;
                    var copy = PublicEntity(e, p.Id); copy.LastSeenTick = state.Tick;
                    for (int m = p.RememberedBuildings.Count - 1; m >= 0; m--)
                        if (p.RememberedBuildings[m].Id == e.Id) p.RememberedBuildings.RemoveAt(m);
                    p.RememberedBuildings.Add(copy);
                }
                p.RememberedBuildings.Sort((a, b) => a.Id.CompareTo(b.Id));
            }
        }
        EntityState PublicEntity(EntityState e, int player)
        {
            // Apply the knowledge policy before cloning: long diagnostic routes and hidden queues are
            // never allocated into a player projection just to be discarded. Future public fields still copy.
            bool own = e.Owner == player;
            var copy = (own ? ownObservationProjector.Value : opponentObservationProjector.Value)(e);
            if (!own) copy.RallyPoint = copy.Position;
            return copy;
        }
        public PlayerObservation Observe(int player)
        {
            if (player < 0 || player >= state.Players.Length) throw new ArgumentOutOfRangeException(nameof(player));
            var p = state.Players[player];
            var result = new PlayerObservation { Tick = state.Tick, Phase = state.Phase, Winner = state.Winner,
                Player = player, Minerals = p.Minerals, Gas = p.Gas, SupplyUsed = p.SupplyUsed, SupplyCap = p.SupplyCap,
                Visible = (bool[])Visibility(player).Clone(), Explored = (bool[])p.Explored.Clone(), CompletedUpgrades = new List<string>(p.CompletedUpgrades) };
            try
            {
                foreach (var e in state.Entities)
                    if (e.Health > 0 && IsVisible(player, e)) observationEntities.Add(e);
                // Capacity is public: size only from revealed entities, never authoritative population.
                result.Entities.Capacity = observationEntities.Count;
                foreach (var e in observationEntities) result.Entities.Add(PublicEntity(e, player));
            }
            finally { observationEntities.Clear(); }
            foreach (var memory in p.RememberedBuildings)
                if (!result.Entities.Any(e => e.Id == memory.Id)) result.RememberedBuildings.Add(CopyEntity(memory));
            return result;
        }
    }
}
