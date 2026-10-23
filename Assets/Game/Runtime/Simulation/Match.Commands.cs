using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        public int Tick => state.Tick;
        string Execute(GameCommand c)
        {
            if (c.Player < 0 || c.Player >= state.Players.Length) return "Invalid player";
            var p = state.Players[c.Player];
            if (c.Sequence <= 0 || c.Sequence <= p.LastProcessedSequence) return "Duplicate or stale sequence";
            p.LastProcessedSequence = c.Sequence;
            p.IssuedSequence = Math.Max(p.IssuedSequence, c.Sequence);
            if (c.Tick < 0) return "Invalid execution tick";
            if (!Enum.IsDefined(typeof(CommandKind), c.Kind)) return "Unknown command";
            if (c.EntityIds == null || c.EntityIds.Length == 0) return "No entities selected";
            if (c.Kind == CommandKind.Research)
            {
                if (c.EntityIds.Distinct().Count() != 1) return "Select one research producer";
                return StartResearch(c.Player, c.EntityIds[0], c.DefinitionId);
            }
            var actors = new List<EntityState>();
            foreach (int id in c.EntityIds.Distinct())
            {
                var e = Entity(id);
                if (e == null || e.Owner != c.Player || Def(e).IsResource) return "Entity missing or not owned";
                actors.Add(e);
            }
            if (c.Kind != CommandKind.Cancel && actors.Any(e => !e.Completed)) return "Entity under construction";
            if (c.Kind == CommandKind.Build) return Build(actors[0], c);
            if (c.Kind == CommandKind.Train) return Train(actors[0], c.DefinitionId);
            if (c.Kind == CommandKind.Cancel) return Cancel(actors[0]);
            if (c.Kind == CommandKind.Stim)
            {
                if (actors.Any(e => Def(e).IsBuilding || Def(e).IsWorker || Def(e).Damage <= 0 || e.Health <= rules.StimHealthCost || e.StimRemaining > 0)) return "Stim unavailable";
                foreach (var e in actors) { e.Health -= rules.StimHealthCost; e.StimRemaining = rules.StimDurationTicks; }
                return null;
            }
            if (c.Kind == CommandKind.Move && actors.All(e => Def(e).IsBuilding))
            {
                if (!Inside(c.Target)) return "Rally point outside map";
                var rallyTarget = Entity(c.TargetId);
                if (c.TargetId != 0 && (rallyTarget == null || !IsVisible(c.Player, rallyTarget) ||
                    (!Def(rallyTarget).IsResource && !IsRefinery(Def(rallyTarget))))) return "Rally target unavailable";
                foreach (var e in actors) { e.RallyPoint = c.Target; e.RallyTargetId = c.TargetId; }
                return null;
            }
            if (actors.Any(e => Def(e).IsBuilding || Def(e).Speed <= 0)) return "Selected entity cannot move";
            bool positionOrder = c.Kind == CommandKind.Move || c.Kind == CommandKind.AttackMove || c.Kind == CommandKind.Patrol;
            if (positionOrder && !Inside(c.Target)) return "Destination outside map";
            var target = Entity(c.TargetId);
            if (c.Kind == CommandKind.Attack && (target == null || target.Owner < 0 || target.Owner == c.Player || !IsVisible(c.Player, target))) return "Attack target not visible";
            if (c.Kind == CommandKind.Gather && (actors.Any(e => !Def(e).IsWorker) || target == null || !CanGather(actors[0], target))) return "Resource unavailable";
            if (c.Kind == CommandKind.Repair && (actors.Any(e => !Def(e).IsWorker || e.Id == c.TargetId) ||
                target == null || target.Owner != c.Player || !target.Completed || !Def(target).Repairable ||
                target.Health >= Def(target).MaxHealth || rules.RepairTicks <= 0 || rules.RepairHealth <= 0)) return "Repair unavailable";
            var destinations = positionOrder ? GroupDestinations(actors, c.Target) : null;
            if (destinations != null && destinations.Count != actors.Count) return "Group destination unavailable";
            foreach (var e in actors)
            {
                var patrolOrigin = e.Position;
                if (c.Kind == CommandKind.Patrol && c.Append && e.Orders.Count > 0)
                {
                    var preceding = e.Orders[e.Orders.Count - 1];
                    if (preceding.Kind == OrderKind.Move || preceding.Kind == OrderKind.AttackMove || preceding.Kind == OrderKind.Patrol)
                        patrolOrigin = preceding.Target;
                }
                if (!c.Append || c.Kind == CommandKind.Stop || c.Kind == CommandKind.Hold) ResetOrders(e);
                if (c.Kind == CommandKind.Stop) continue;
                var kind = c.Kind == CommandKind.Move ? OrderKind.Move : c.Kind == CommandKind.AttackMove ? OrderKind.AttackMove :
                    c.Kind == CommandKind.Attack ? OrderKind.Attack : c.Kind == CommandKind.Gather ? OrderKind.Gather :
                    c.Kind == CommandKind.Patrol ? OrderKind.Patrol : c.Kind == CommandKind.Repair ? OrderKind.Repair : OrderKind.Hold;
                e.Orders.Add(new UnitOrder { Kind = kind, Target = destinations == null ? c.Target : destinations[e.Id],
                    TargetId = c.TargetId, PatrolOrigin = c.Kind == CommandKind.Patrol ? patrolOrigin : default });
                if (kind == OrderKind.Gather) e.GatherTargetId = c.TargetId;
            }
            return null;
        }

        void ResetOrders(EntityState entity) { entity.Orders.Clear(); ClearPath(entity); entity.WorkTimer = 0; entity.AvoidanceSide = 0; }
        bool HasPrerequisite(int owner, EntityDefinition d) => string.IsNullOrEmpty(d.PrerequisiteId) || state.Entities.Any(e => e.Owner == owner && e.Completed && e.Health > 0 && e.DefinitionId == d.PrerequisiteId);
        bool Afford(PlayerState p, EntityDefinition d) => p.Minerals >= d.MineralCost && p.Gas >= d.GasCost;
        void Pay(PlayerState p, EntityDefinition d) { p.Minerals -= d.MineralCost; p.Gas -= d.GasCost; }
        string Build(EntityState worker, GameCommand c)
        {
            if (!Def(worker).IsWorker) return "Builder must be worker";
            if (c.TargetId > 0)
            {
                var unfinished = Entity(c.TargetId);
                if (unfinished == null || unfinished.Owner != worker.Owner || unfinished.Completed || !Def(unfinished).IsBuilding) return "No owned unfinished building";
                unfinished.BuilderId = worker.Id;
                ResetOrders(worker);
                worker.Orders.Add(new UnitOrder { Kind = OrderKind.Build, TargetId = unfinished.Id, Target = unfinished.Position });
                return null;
            }
            if (!definitions.TryGetValue(c.DefinitionId ?? "", out var d) || !d.IsBuilding || d.IsResource) return "Invalid building definition";
            if (!HasPrerequisite(worker.Owner, d)) return "Prerequisite missing";
            if (!CanPlace(d.Id, c.Target, worker.Owner)) return "Placement blocked";
            var p = state.Players[worker.Owner];
            if (!Afford(p, d)) return "Insufficient resources";
            Pay(p, d);
            var building = Spawn(d.Id, worker.Owner, c.Target, false);
            building.BuilderId = worker.Id;
            ResetOrders(worker);
            worker.Orders.Add(new UnitOrder { Kind = OrderKind.Build, TargetId = building.Id, Target = c.Target });
            return null;
        }
        string Train(EntityState producer, string id)
        {
            if (!definitions.TryGetValue(id ?? "", out var d) || !CanProduce(producer, d)) return "Invalid producer or unit";
            if (!producer.Completed) return "Producer incomplete";
            if (producer.Research.Count > 0) return "Producer busy";
            if (!HasPrerequisite(producer.Owner, d)) return "Prerequisite missing";
            var p = state.Players[producer.Owner];
            if (!Afford(p, d)) return "Insufficient resources";
            RecalculateSupply();
            if (p.SupplyUsed + d.SupplyCost > p.SupplyCap) return "Supply blocked";
            Pay(p, d);
            producer.Production.Add(new ProductionItem { DefinitionId = d.Id, RemainingTicks = d.BuildTicks,
                TotalTicks = d.BuildTicks, PaidMinerals = d.MineralCost, PaidGas = d.GasCost });
            RecalculateSupply();
            return null;
        }
        bool CanProduce(EntityState producer, EntityDefinition unit) =>
            Def(producer).IsBuilding && !Def(producer).IsResource &&
            !unit.IsBuilding && !unit.IsResource && unit.ProducerId == producer.DefinitionId;

        void ValidateProductionState(EntityState producer)
        {
            if (producer.Production.Count == 0) return;
            if (producer.Owner < 0 || !producer.Completed || producer.Research.Count != 0)
                throw new ArgumentException("Invalid or occupied production producer");
            for (int index = 0; index < producer.Production.Count; index++)
            {
                var item = producer.Production[index];
                if (item == null || !definitions.TryGetValue(item.DefinitionId ?? "", out var unit) || !CanProduce(producer, unit) ||
                    item.TotalTicks != unit.BuildTicks || item.RemainingTicks < 0 || item.RemainingTicks > item.TotalTicks ||
                    (index > 0 && item.RemainingTicks != item.TotalTicks) ||
                    item.PaidMinerals != unit.MineralCost || item.PaidGas != unit.GasCost)
                    throw new ArgumentException("Malformed production queue or payment");
            }
            // A completed head may wait at zero until an ordinary spawn point becomes free.
            // Start-time funds/prerequisites are not rechecked: payment already happened.
        }
        string Cancel(EntityState e)
        {
            var p = state.Players[e.Owner];
            if (!e.Completed)
            {
                var d = Def(e); p.Minerals += d.MineralCost; p.Gas += d.GasCost; e.Health = 0;
                InvalidateNavigation();
                var worker = Entity(e.BuilderId);
                if (worker != null) { worker.Orders.RemoveAll(o => o.Kind == OrderKind.Build && o.TargetId == e.Id); ClearPath(worker); }
            }
            else if (e.Research.Count > 0)
            {
                var item = e.Research[0]; e.Research.Clear();
                p.Minerals += item.PaidMinerals; p.Gas += item.PaidGas;
            }
            else if (e.Production.Count > 0)
            {
                int i = e.Production.Count - 1; var item = e.Production[i]; e.Production.RemoveAt(i);
                p.Minerals += item.PaidMinerals; p.Gas += item.PaidGas;
            }
            else return "Nothing to cancel";
            RecalculateSupply(); return null;
        }

        public bool CanPlace(string definitionId, SimPoint position, int player)
        {
            if (player < 0 || player > 1 || !definitions.TryGetValue(definitionId ?? "", out var d) || !d.IsBuilding || d.IsResource || !Inside(position, d.Radius)) return false;
            // Knowledge gate precedes all live occupancy checks: preview and command failure cannot probe fog.
            if (!PlacementVisible(position, d.Radius, player)) return false;
            if (BlockedTerrain(position, d.Radius)) return false;
            bool refinery = IsRefinery(d);
            bool gas = false;
            foreach (var e in state.Entities)
            {
                if (e.Health <= 0) continue;
                if (refinery && IsGasNode(e) && Near(position, e.Position, 500)) { gas = true; continue; }
                if (DistanceSquared(position, e.Position) < (long)(d.Radius + Def(e).Radius) * (d.Radius + Def(e).Radius)) return false;
            }
            return !refinery || gas;
        }
        bool PlacementVisible(SimPoint position, int radius, int player)
        {
            var visible = Visibility(player);
            int minX = Math.Max(0, (position.X - radius) / 1000), maxX = Math.Min(rules.MapWidth - 1, (position.X + radius) / 1000);
            int minZ = Math.Max(0, (position.Z - radius) / 1000), maxZ = Math.Min(rules.MapHeight - 1, (position.Z + radius) / 1000);
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                int nearestX = Math.Max(x * 1000, Math.Min(position.X, (x + 1) * 1000));
                int nearestZ = Math.Max(z * 1000, Math.Min(position.Z, (z + 1) * 1000));
                if (DistanceSquared(position, new SimPoint(nearestX, nearestZ)) < (long)radius * radius && !visible[z * rules.MapWidth + x]) return false;
            }
            return true;
        }
        static bool IsRefinery(EntityDefinition d) => d.Id.IndexOf("refinery", StringComparison.OrdinalIgnoreCase) >= 0;
        bool IsGasNode(EntityState e) => Def(e).IsResource && (e.DefinitionId.IndexOf("geyser", StringComparison.OrdinalIgnoreCase) >= 0 || e.DefinitionId.IndexOf("gas", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
