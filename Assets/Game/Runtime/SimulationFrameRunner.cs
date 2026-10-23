using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ColonyRTS.Simulation;

namespace ColonyRTS
{
    public readonly struct ObservedCombatStats
    {
        public int Damage { get; }
        public int Armor { get; }
        public ObservedCombatStats(int damage, int armor) { Damage = damage; Armor = armor; }
    }

    /// <summary>Detached publication. Its consumer must treat this payload as read-only.</summary>
    public sealed class SimulationFrame
    {
        public PlayerObservation Observation;
        public List<CommandResult> Results = new List<CommandResult>();
        public int CompletedSteps;
        public bool ReplayEnded;
        public double ComputeMilliseconds;
        // Opt-in diagnostic data; never consumed by fog-filtered product UI.
        public MatchCombatDiagnostics CombatDiagnostics;
        public Dictionary<string, ObservedCombatStats> OwnCombatStats =
            new Dictionary<string, ObservedCombatStats>(StringComparer.Ordinal);
        public Dictionary<int, Dictionary<string, CommandResult>> ResearchAvailability =
            new Dictionary<int, Dictionary<string, CommandResult>>();
        public Dictionary<string, CommandResult> MissingProducerAvailability =
            new Dictionary<string, CommandResult>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Runs one fixed-step batch without a clock or Unity API. The caller exclusively owns
    /// Match before scheduling and after completion; it must not access Match while pending.
    /// The supplied rules must be the matching immutable catalog for that Match.
    /// </summary>
    public sealed class SimulationFrameRunner : IDisposable
    {
        readonly object gate = new object();
        Task<SimulationFrame> pending;
        bool disposed;

        // A finished but unpublished task still owns the handoff until consumed.
        public bool HasPending { get { lock (gate) return pending != null; } }

        public Task<SimulationFrame> Schedule(Match match, RuleSet immutableRules, int steps, int replayEndTick = -1, bool captureCombatDiagnostics = false)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (immutableRules == null) throw new ArgumentNullException(nameof(immutableRules));
            if (immutableRules.Definitions == null) throw new ArgumentException("A definition catalog is required.", nameof(immutableRules));
            if (steps < 0) throw new ArgumentOutOfRangeException(nameof(steps));
            if (replayEndTick < -1) throw new ArgumentOutOfRangeException(nameof(replayEndTick));
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(SimulationFrameRunner));
                if (pending != null) throw new InvalidOperationException("Consume the pending simulation frame before scheduling another batch.");
                pending = Task.Run(() => Advance(match, immutableRules, steps, replayEndTick, captureCombatDiagnostics));
                return pending;
            }
        }

        public bool TryComplete(out SimulationFrame frame)
        {
            lock (gate)
            {
                frame = null;
                if (pending == null || !pending.IsCompleted) return false;
                frame = CompletePending();
                return true;
            }
        }

        /// <summary>Waits for and consumes a pending publication; returns null when idle.</summary>
        public SimulationFrame Complete()
        {
            lock (gate) return CompletePending();
        }

        SimulationFrame CompletePending()
        {
            if (pending == null) return null;
            try { return pending.GetAwaiter().GetResult(); }
            finally { pending = null; }
        }

        static SimulationFrame Advance(Match match, RuleSet rules, int steps, int replayEndTick, bool captureCombatDiagnostics)
        {
            long started = Stopwatch.GetTimestamp();
            var frame = new SimulationFrame();
            for (int step = 0; step < steps; step++)
            {
                if (replayEndTick >= 0 && match.Tick >= replayEndTick) break;
                int before = match.Tick;
                match.Step();
                // Finished matches do not advance. No full-state clone is needed to test phase.
                if (match.Tick == before) break;
                frame.CompletedSteps++;
                foreach (var result in match.LastResults)
                    frame.Results.Add(new CommandResult { Player = result.Player, Sequence = result.Sequence,
                        Accepted = result.Accepted, Reason = result.Reason });
            }
            frame.Observation = match.Observe(0);
            if(captureCombatDiagnostics)frame.CombatDiagnostics=match.CaptureCombatDiagnostics();
            frame.ReplayEnded = replayEndTick >= 0 && match.Tick >= replayEndTick;
            foreach (var definition in rules.Definitions)
                frame.OwnCombatStats.Add(definition.Id,
                    new ObservedCombatStats(match.CombatDamage(0, definition.Id), match.CombatArmor(0, definition.Id)));
            var researchByProducer = new Dictionary<string, List<UpgradeDefinition>>(StringComparer.Ordinal);
            foreach (var upgrade in rules.Upgrades ?? new UpgradeDefinition[0])
            {
                frame.MissingProducerAvailability.Add(upgrade.Id, match.ResearchAvailability(0, 0, upgrade.Id));
                if (!researchByProducer.TryGetValue(upgrade.ProducerId, out var available))
                {
                    available = new List<UpgradeDefinition>();
                    researchByProducer.Add(upgrade.ProducerId, available);
                }
                available.Add(upgrade);
            }
            foreach (var entity in frame.Observation.Entities)
            {
                if (entity.Owner != 0 || !researchByProducer.TryGetValue(entity.DefinitionId, out var upgrades)) continue;
                var gates = new Dictionary<string, CommandResult>(StringComparer.Ordinal);
                foreach (var upgrade in upgrades)
                    gates.Add(upgrade.Id, match.ResearchAvailability(0, entity.Id, upgrade.Id));
                frame.ResearchAvailability.Add(entity.Id, gates);
            }
            frame.ComputeMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            return frame;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                CompletePending();
            }
        }
    }
}
