using System;
using System.Collections.Generic;

namespace ColonyRTS.Simulation
{
    /// <summary>Detached, derived diagnostic totals. They are not saved or included in gameplay hashes.</summary>
    [Serializable]
    public struct MatchCombatDiagnostics
    {
        public int Tick;
        public long Shots0, Shots1;
        public long HealthDamage0, HealthDamage1;
        // Deaths belong to the victim's owner; shots and damage belong to the attacker.
        public long Deaths0, Deaths1;
        public int LiveMarines0, LiveMarines1;
        public long MarineHealth0, MarineHealth1;
        public long MarineTravel0, MarineTravel1;
        public int MovedMarines0, MovedMarines1;
        public int TickMovingMarines0, TickMovingMarines1;
        public int BotThinkCount, BotAcceptedCommands;
        public int BotAttackMoveCommands, BotAttackMoveUnits;
        public int BotBuildCommands, BotGatherCommands, BotTrainCommands;
    }

    public sealed partial class Match
    {
        readonly long[] diagnosticShots = new long[2];
        readonly long[] diagnosticHealthDamage = new long[2];
        readonly long[] diagnosticDeaths = new long[2];
        DetailedDiagnosticCounters detailedDiagnostics;
        bool detailedDiagnosticsEnabled;
        /// <summary>Opt-in derived movement/AI instrumentation, excluded from saves and gameplay hashes.</summary>
        public bool DetailedDiagnosticsEnabled
        {
            get => detailedDiagnosticsEnabled;
            set
            {
                // Prepare the common diagnostic workload before its timed steps. Larger armies grow normally.
                if (value) DetailedCounters();
                detailedDiagnosticsEnabled = value;
            }
        }

        sealed class DetailedDiagnosticCounters
        {
            public readonly long[] Travel = new long[2];
            public readonly HashSet<int>[] Moved = { new HashSet<int>(400), new HashSet<int>(400) };
            public readonly HashSet<int>[] TickMoved = { new HashSet<int>(400), new HashSet<int>(400) };
            public int Thinks, Accepted, AttackMoves, AttackMoveUnits, Builds, Gathers, Trains;
        }
        DetailedDiagnosticCounters DetailedCounters() => detailedDiagnostics ?? (detailedDiagnostics = new DetailedDiagnosticCounters());
        void BeginDetailedDiagnosticStep()
        {
            var counters = DetailedCounters();
            counters.TickMoved[0].Clear(); counters.TickMoved[1].Clear();
        }
        void RecordMarineMovement(EntityState entity, SimPoint destination)
        {
            if (entity.DefinitionId != "marine" || entity.Owner < 0 || entity.Owner > 1) return;
            int distance = Root(DistanceSquared(entity.Position, destination));
            if (distance <= 0) return;
            var counters = DetailedCounters();
            counters.Travel[entity.Owner] += distance;
            counters.Moved[entity.Owner].Add(entity.Id);
            counters.TickMoved[entity.Owner].Add(entity.Id);
        }
        void RecordBotAcceptedCommand(GameCommand command)
        {
            // The profiling driver issues player 0 only; enabled player 1 is the ordinary bot.
            if (command.Player != 1) return;
            bool enabled = false;
            foreach (var bot in state.Bots) if (bot.Player == 1 && bot.Enabled) { enabled = true; break; }
            if (!enabled) return;
            var counters = DetailedCounters();
            counters.Accepted++;
            switch (command.Kind)
            {
                case CommandKind.AttackMove: counters.AttackMoves++; counters.AttackMoveUnits += command.EntityIds.Length; break;
                case CommandKind.Build: counters.Builds++; break;
                case CommandKind.Gather: counters.Gathers++; break;
                case CommandKind.Train: counters.Trains++; break;
            }
        }

        void RecordCombatShot(EntityState attacker, EntityState target, int healthBefore)
        {
            if (attacker.Owner >= 0 && attacker.Owner < 2)
            {
                diagnosticShots[attacker.Owner]++;
                diagnosticHealthDamage[attacker.Owner] += healthBefore - Math.Max(0, target.Health);
            }
            if (healthBefore > 0 && target.Health <= 0 && target.Owner >= 0 && target.Owner < 2)
                diagnosticDeaths[target.Owner]++;
        }

        /// <summary>
        /// Nonmutating capture while the caller owns Match. Totals start at this Match's creation,
        /// including after Restore; live marine counts/HP reflect the current authoritative state.
        /// HealthDamage excludes overkill. Deaths count owned combat victims of every definition.
        /// </summary>
        public MatchCombatDiagnostics CaptureCombatDiagnostics()
        {
            var result = new MatchCombatDiagnostics
            {
                Tick = state.Tick,
                Shots0 = diagnosticShots[0], Shots1 = diagnosticShots[1],
                HealthDamage0 = diagnosticHealthDamage[0], HealthDamage1 = diagnosticHealthDamage[1],
                Deaths0 = diagnosticDeaths[0], Deaths1 = diagnosticDeaths[1]
            };
            var counters = detailedDiagnostics;
            if (counters != null)
            {
                result.MarineTravel0 = counters.Travel[0]; result.MarineTravel1 = counters.Travel[1];
                result.MovedMarines0 = counters.Moved[0].Count; result.MovedMarines1 = counters.Moved[1].Count;
                if (DetailedDiagnosticsEnabled)
                {
                    result.TickMovingMarines0 = counters.TickMoved[0].Count;
                    result.TickMovingMarines1 = counters.TickMoved[1].Count;
                }
                result.BotThinkCount = counters.Thinks; result.BotAcceptedCommands = counters.Accepted;
                result.BotAttackMoveCommands = counters.AttackMoves; result.BotAttackMoveUnits = counters.AttackMoveUnits;
                result.BotBuildCommands = counters.Builds; result.BotGatherCommands = counters.Gathers; result.BotTrainCommands = counters.Trains;
            }
            foreach (var entity in state.Entities)
            {
                if (entity.Health <= 0 || entity.DefinitionId != "marine") continue;
                if (entity.Owner == 0) { result.LiveMarines0++; result.MarineHealth0 += entity.Health; }
                else if (entity.Owner == 1) { result.LiveMarines1++; result.MarineHealth1 += entity.Health; }
            }
            return result;
        }
    }
}
