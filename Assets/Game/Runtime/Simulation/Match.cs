using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyRTS.Simulation
{
    /// <summary>Authoritative, single-threaded fixed-step gameplay. Coordinates and timers are integral.</summary>
    public sealed partial class Match
    {
        RuleSet rules;
        MatchState state;
        readonly Dictionary<string, EntityDefinition> definitions = new Dictionary<string, EntityDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string, UpgradeDefinition> upgrades = new Dictionary<string, UpgradeDefinition>(StringComparer.Ordinal);
        readonly List<CommandResult> results = new List<CommandResult>();
        bool replaying;
        public MatchState State => CaptureState();
        public RuleSet Rules => CopyRules(rules);
        public IReadOnlyList<CommandResult> LastResults => results.AsReadOnly();

        public Match(RuleSet rules, bool enableEnemyAI = true, int seed = 42)
        {
            ValidateRules(rules);
            this.rules = CopyRules(rules);
            this.rules.Upgrades = this.rules.Upgrades ?? new UpgradeDefinition[0];
            foreach (var d in this.rules.Definitions) definitions.Add(d.Id, d);
            foreach (var upgrade in this.rules.Upgrades) upgrades.Add(upgrade.Id, upgrade);
            state = new MatchState { RuleVersion = rules.VersionId, RuleHash = RulesHash(this.rules), Seed = seed,
                RngState = (uint)seed, Phase = MatchPhase.Running,
                Players = new[] { NewPlayer(0), NewPlayer(1) },
                Bots = new[] { new BotState { Player = 1, Enabled = enableEnemyAI } } };
            foreach (var spawn in rules.Spawns) Spawn(spawn.DefinitionId, spawn.Owner, spawn.Position, true);
            RecalculateSupply();
            RefreshKnowledge();
            foreach (var worker in state.Entities.Where(e => Def(e).IsWorker)) AssignMining(worker);
        }

        PlayerState NewPlayer(int id) => new PlayerState { Id = id, Minerals = rules.StartingMinerals, Gas = rules.StartingGas,
            Explored = new bool[rules.MapWidth * rules.MapHeight] };
        EntityDefinition Def(EntityState e) => definitions[e.DefinitionId];
        EntityState Entity(int id)
        {
            foreach (var entity in state.Entities) if (entity.Id == id && entity.Health > 0) return entity;
            return null;
        }
        public EntityState Find(int id) { var e = Entity(id); return e == null ? null : CopyEntity(e); }
        public EntityDefinition Definition(string id) => definitions.TryGetValue(id ?? "", out var d) ? CopyDefinition(d) : null;
        static long DistanceSquared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }
        static bool Near(SimPoint a, SimPoint b, int range) => DistanceSquared(a, b) <= (long)range * range;
        bool Inside(SimPoint p, int radius = 0) => p.X >= radius && p.Z >= radius && p.X < rules.MapWidth * 1000 - radius && p.Z < rules.MapHeight * 1000 - radius;
        EntityState Spawn(string id, int owner, SimPoint position, bool completed)
        {
            var d = definitions[id];
            var e = new EntityState { Id = state.NextEntityId++, DefinitionId = id, Owner = owner, Position = position,
                Health = completed ? d.MaxHealth : Math.Max(1, d.MaxHealth / 10), Completed = completed,
                ConstructionRemaining = completed ? 0 : d.BuildTicks, ConstructionTotal = d.BuildTicks,
                ResourceRemaining = d.ResourceAmount, RallyPoint = position };
            state.Entities.Add(e);
            if (d.IsBuilding || d.IsResource) InvalidateNavigation();
            return e;
        }

        public GameCommand MakeCommand(int player, CommandKind kind, int[] ids = null, SimPoint target = default,
            int targetId = 0, string definitionId = null, bool append = false)
        {
            if (player < 0 || player >= state.Players.Length) throw new ArgumentOutOfRangeException(nameof(player));
            return new GameCommand { Player = player, Sequence = ++state.Players[player].IssuedSequence, Tick = state.Tick,
                Kind = kind, EntityIds = ids == null ? new int[0] : (int[])ids.Clone(), Target = target,
                TargetId = targetId, DefinitionId = definitionId, Append = append };
        }
        public void QueueCommand(GameCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            state.PendingCommands.Add(CopyCommand(command));
        }
        public void Step()
        {
            results.Clear();
            if (state.Phase != MatchPhase.Running) return;
            if (DetailedDiagnosticsEnabled) BeginDetailedDiagnosticStep();
            visibilityTicks[0] = visibilityTicks[1] = -1;
            combatIndexTick = -1;
            advancingState = true;
            try
            {
                if (!replaying) ThinkBots();
                var ready = state.PendingCommands.Where(c => c.Tick <= state.Tick).ToList();
                state.PendingCommands.RemoveAll(c => c.Tick <= state.Tick);
                foreach (var command in ready)
                {
                    string reason = Execute(command);
                    results.Add(new CommandResult { Player = command.Player, Sequence = command.Sequence, Accepted = reason == null, Reason = reason ?? "Accepted" });
                    if (reason == null)
                    {
                        if (DetailedDiagnosticsEnabled) RecordBotAcceptedCommand(command);
                        var logged = CopyCommand(command); logged.Tick = state.Tick; state.CommandLog.Add(logged);
                    }
                }
                TickConstructionAndProduction();
                // Spawn only appends; physical removal/reordering happens after this phase.
                // Capture after production so newborns retain their same-tick order/combat turn.
                int orderCount = state.Entities.Count;
                for (int index = 0; index < orderCount; index++)
                {
                    var e = state.Entities[index];
                    if (e.Health <= 0 || !e.Completed || Def(e).IsResource) continue;
                    if (e.AttackTimer > 0) e.AttackTimer--;
                    if (e.StimRemaining > 0) e.StimRemaining--;
                    TickOrder(e);
                }
                state.Entities.RemoveAll(e => e.Health <= 0);
                RecalculateSupply();
                state.Tick++;
                advancingState = false;
                RefreshKnowledge();
                CheckVictory();
            }
            finally
            {
                advancingState = false;
            }
        }
        void CheckVictory()
        {
            bool a = state.Entities.Any(e => e.Owner == 0 && Def(e).IsBase && e.Health > 0);
            bool b = state.Entities.Any(e => e.Owner == 1 && Def(e).IsBase && e.Health > 0);
            if (!a || !b) { state.Phase = MatchPhase.Finished; state.Winner = a == b ? -1 : a ? 0 : 1; }
        }
        void RecalculateSupply()
        {
            foreach (var p in state.Players)
            {
                p.SupplyCap = 0; p.SupplyUsed = 0;
                foreach (var e in state.Entities)
                {
                    if (e.Health <= 0 || e.Owner != p.Id) continue;
                    var d = Def(e);
                    if (e.Completed) p.SupplyCap += d.SupplyProvided;
                    p.SupplyUsed += d.SupplyCost;
                    foreach (var item in e.Production) p.SupplyUsed += definitions[item.DefinitionId].SupplyCost;
                }
            }
        }
    }
}
