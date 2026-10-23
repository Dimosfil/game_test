using System;
using System.Linq;

namespace ColonyRTS.Simulation
{
    public sealed partial class Match
    {
        void ThinkBots()
        {
            foreach (var bot in state.Bots)
            {
                if (!bot.Enabled || state.Tick < bot.NextThinkTick) continue;
                bot.NextThinkTick = state.Tick + rules.TickRate;
                if (DetailedDiagnosticsEnabled) DetailedCounters().Thinks++;
                Think(bot, Observe(bot.Player));
            }
        }
        void BotCommand(BotState bot, CommandKind kind, int id, string definition = null, int targetId = 0, SimPoint target = default)
            => QueueCommand(MakeCommand(bot.Player, kind, new[] { id }, target, targetId, definition));
        void Think(BotState bot, PlayerObservation observation)
        {
            var own = observation.Entities.Where(e => e.Owner == bot.Player).ToList();
            var home = own.FirstOrDefault(e => Def(e).IsBase && e.Completed);
            if (home == null) return;
            var workers = own.Where(e => Def(e).IsWorker && e.Completed).ToList();
            var minerals = observation.Entities.Where(e => Def(e).IsResource && !IsGasNode(e) && e.ResourceRemaining > 0).ToList();
            foreach (var worker in workers)
            {
                if (worker.Orders.Count > 0 || minerals.Count == 0) continue;
                var mine = minerals.OrderBy(e => DistanceSquared(e.Position, worker.Position)).ThenBy(e => e.Id).First();
                BotCommand(bot, CommandKind.Gather, worker.Id, targetId: mine.Id);
            }
            var workerDef = definitions.Values.FirstOrDefault(d => d.IsWorker && d.ProducerId == home.DefinitionId);
            if (workerDef != null && workers.Count + own.Sum(e => e.Production.Count(p => p.DefinitionId == workerDef.Id)) < 10 && home.Production.Count == 0)
                BotCommand(bot, CommandKind.Train, home.Id, workerDef.Id);

            var builder = workers.FirstOrDefault(e => e.Orders.Count == 0 || e.Orders[0].Kind == OrderKind.Gather);
            var supply = definitions.Values.Where(d => d.IsBuilding && !d.IsBase && d.SupplyProvided > 0).OrderBy(d => d.MineralCost).FirstOrDefault();
            if (builder != null && supply != null && observation.SupplyCap - observation.SupplyUsed <= 3 && !own.Any(e => e.DefinitionId == supply.Id && !e.Completed))
            { if (BotBuild(bot, builder, home.Position, supply)) return; }

            var soldier = definitions.Values.Where(d => !d.IsBuilding && !d.IsWorker && !d.IsResource && d.Damage > 0 && !string.IsNullOrEmpty(d.ProducerId))
                .OrderBy(d => d.MineralCost).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
            if (soldier == null) return;
            var producers = own.Where(e => e.DefinitionId == soldier.ProducerId).ToList();
            if (builder != null && producers.Count < (state.Tick > rules.TickRate * 120 ? 2 : 1) && definitions.TryGetValue(soldier.ProducerId, out var producerDef))
            { if (BotBuild(bot, builder, home.Position, producerDef)) return; }
            foreach (var producer in producers)
                if (producer.Completed && producer.Production.Count < 2) BotCommand(bot, CommandKind.Train, producer.Id, soldier.Id);

            var fighters = own.Where(e => e.Completed && !Def(e).IsBuilding && !Def(e).IsWorker && Def(e).Damage > 0).ToList();
            if (fighters.Count < 4) return;
            var known = observation.Entities.Concat(observation.RememberedBuildings).Where(e => e.Owner >= 0 && e.Owner != bot.Player)
                .OrderByDescending(e => Def(e).IsBase).ThenBy(e => DistanceSquared(e.Position, home.Position)).FirstOrDefault();
            var marching = fighters.FirstOrDefault(e => e.Orders.Count > 0 && e.Orders[0].Kind == OrderKind.AttackMove);
            SimPoint goal;
            bool newScoutWave = false;
            if (known != null) goal = known.Position;
            else if (marching != null) goal = marching.Orders[0].Target;
            else
            {
                newScoutWave = true;
                int margin = 4000;
                var corners = new[] { new SimPoint(margin, margin), new SimPoint(rules.MapWidth * 1000 - margin, margin),
                    new SimPoint(margin, rules.MapHeight * 1000 - margin), new SimPoint(rules.MapWidth * 1000 - margin, rules.MapHeight * 1000 - margin) }
                    .OrderByDescending(p => DistanceSquared(p, home.Position)).ToArray();
                goal = corners[bot.AttackWave % corners.Length];
                if (Near(home.Position, goal, 10000)) { bot.AttackWave++; goal = corners[bot.AttackWave % corners.Length]; }
            }
            var idle = fighters.Where(e => e.Orders.Count == 0 || e.Orders[0].Kind == OrderKind.Hold).Select(e => e.Id).ToArray();
            if (idle.Length > 0) { QueueCommand(MakeCommand(bot.Player, CommandKind.AttackMove, idle, goal)); if (newScoutWave) bot.AttackWave++; }
        }
        bool BotBuild(BotState bot, EntityState builder, SimPoint center, EntityDefinition definition)
        {
            if (!Afford(state.Players[bot.Player], definition) || !HasPrerequisite(bot.Player, definition)) return false;
            for (int radius = 5000; radius <= 14000; radius += 3000)
                for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;
                    var position = new SimPoint(center.X + x * radius, center.Z + z * radius);
                    if (!CanPlace(definition.Id, position, bot.Player)) continue;
                    BotCommand(bot, CommandKind.Build, builder.Id, definition.Id, target: position); return true;
                }
            return false;
        }
    }
}
