using System;
using System.Linq;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        bool CanGather(EntityState worker, EntityState target)
        {
            if (!Def(worker).IsWorker || !IsVisible(worker.Owner, target)) return false;
            var node = ResourceNode(target);
            if (node == null || node.ResourceRemaining <= 0) return false;
            if (!IsGasNode(node)) return true;
            return state.Entities.Any(e => e.Owner == worker.Owner && e.Health > 0 && e.Completed && IsRefinery(Def(e)) && Near(e.Position, node.Position, 500));
        }
        EntityState ResourceNode(EntityState target)
        {
            if (Def(target).IsResource) return target;
            if (!IsRefinery(Def(target))) return null;
            return state.Entities.Find(e => IsGasNode(e) && Near(e.Position, target.Position, 500));
        }
        void TickConstructionAndProduction()
        {
            // Production can append newborns but never removes/reorders entities. Preserve the
            // phase-entry population without a temporary snapshot or processing those births twice.
            int productionCount = state.Entities.Count;
            for (int index = 0; index < productionCount; index++)
            {
                var e = state.Entities[index];
                if (e.Health <= 0) continue;
                var d = Def(e);
                if (!e.Completed)
                {
                    var worker = Entity(e.BuilderId);
                    if (worker == null || !worker.Completed || !Def(worker).IsWorker || worker.Owner != e.Owner ||
                        worker.Orders.Count == 0 || worker.Orders[0].Kind != OrderKind.Build || worker.Orders[0].TargetId != e.Id ||
                        !Near(worker.Position, e.Position, d.Radius + Def(worker).Radius + 300)) continue;
                    e.ConstructionRemaining--;
                    int before = e.ConstructionTotal - e.ConstructionRemaining - 1;
                    int added = d.MaxHealth * (before + 1) / Math.Max(1, e.ConstructionTotal) - d.MaxHealth * before / Math.Max(1, e.ConstructionTotal);
                    e.Health = Math.Min(d.MaxHealth, e.Health + added);
                    if (e.ConstructionRemaining <= 0) { e.Completed = true; e.ConstructionRemaining = 0; e.Health = Math.Min(d.MaxHealth, e.Health); }
                    continue;
                }
                TickResearch(e);
                if (e.Production.Count == 0) continue;
                var item = e.Production[0];
                if (item.RemainingTicks > 0) item.RemainingTicks--;
                if (item.RemainingTicks > 0) continue;
                var unit = definitions[item.DefinitionId];
                if (!TrySpawnPoint(e, unit, out var position)) continue;
                e.Production.RemoveAt(0);
                var created = Spawn(unit.Id, e.Owner, position, true);
                var rallyResource = Entity(e.RallyTargetId);
                if (unit.IsWorker && rallyResource != null && CanGather(created, rallyResource))
                {
                    created.GatherTargetId = rallyResource.Id;
                    created.Orders.Add(new UnitOrder { Kind = OrderKind.Gather, TargetId = rallyResource.Id });
                }
                else if (!Near(e.RallyPoint, e.Position, d.Radius)) created.Orders.Add(new UnitOrder { Kind = OrderKind.Move, Target = e.RallyPoint });
                else if (unit.IsWorker) AssignMining(created);
            }
        }
        bool TrySpawnPoint(EntityState producer, EntityDefinition unit, out SimPoint point)
        {
            int distance = Def(producer).Radius + unit.Radius + 200;
            for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i < 8; i++)
                {
                    int[] x = { 1, 1, 0, -1, -1, -1, 0, 1 }, z = { 0, 1, 1, 1, 0, -1, -1, -1 };
                    point = new SimPoint(producer.Position.X + x[i] * (distance + ring * 1000), producer.Position.Z + z[i] * (distance + ring * 1000));
                    var probe = new EntityState { Id = -1, DefinitionId = unit.Id, Position = point };
                    if (Walkable(point, unit.Radius) && UnitsClear(probe, point)) return true;
                }
            point = default; return false;
        }
        void AssignMining(EntityState worker)
        {
            var mine = state.Entities.Where(e => Def(e).IsResource && !IsGasNode(e) && e.ResourceRemaining > 0 && IsVisible(worker.Owner, e))
                .OrderBy(e => DistanceSquared(e.Position, worker.Position)).ThenBy(e => e.Id).FirstOrDefault();
            if (mine == null) return;
            worker.GatherTargetId = mine.Id;
            worker.Orders.Add(new UnitOrder { Kind = OrderKind.Gather, TargetId = mine.Id });
        }
        void Gather(EntityState worker, UnitOrder order)
        {
            if (worker.Cargo > 0)
            {
                var home = state.Entities.Where(e => e.Owner == worker.Owner && e.Health > 0 && e.Completed && Def(e).IsBase)
                    .OrderBy(e => DistanceSquared(e.Position, worker.Position)).ThenBy(e => e.Id).FirstOrDefault();
                if (home == null) return;
                if (!MoveToward(worker, home.Position, Def(home).Radius + Def(worker).Radius + 250)) return;
                if (worker.GasCargo) state.Players[worker.Owner].Gas += worker.Cargo;
                else state.Players[worker.Owner].Minerals += worker.Cargo;
                worker.Cargo = 0; worker.WorkTimer = 0; ClearPath(worker);
                return;
            }
            var target = Entity(order.TargetId);
            var node = target == null ? null : ResourceNode(target);
            if (node == null || !CanGather(worker, target)) { CompleteOrder(worker); return; }
            int approach = Def(node).Radius + Def(worker).Radius + 250;
            if (IsGasNode(node))
            {
                var refinery = state.Entities.Find(e => e.Owner == worker.Owner && e.Completed && e.Health > 0 && IsRefinery(Def(e)) && Near(e.Position, node.Position, 500));
                if (refinery == null) { CompleteOrder(worker); return; }
                approach = Math.Max(approach, Def(refinery).Radius + Def(worker).Radius + 250);
            }
            if (!MoveToward(worker, node.Position, approach)) { worker.WorkTimer = 0; return; }
            worker.WorkTimer++;
            if (worker.WorkTimer < rules.GatherTicks) return;
            worker.WorkTimer = 0;
            worker.Cargo = Math.Min(rules.CargoAmount, node.ResourceRemaining);
            node.ResourceRemaining -= worker.Cargo; worker.GasCargo = IsGasNode(node); ClearPath(worker);
        }
    }
}
