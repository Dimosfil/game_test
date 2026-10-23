using System;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class SimulationFrameRunnerTests
    {
        [Test, Timeout(15000)]
        public void WorkerMatchesOneHundredTicksOfCommandsSaveContinuationAndReplay()
        {
            var rules = SimulationFixture.Rules();
            var synchronous = new Match(rules, true, 814);
            var asynchronous = new Match(rules, true, 814);
            using (var runner = new SimulationFrameRunner())
            {
                for (int batch = 0; batch < 20; batch++)
                {
                    int worker = SimulationFixture.Id(synchronous, "worker");
                    var kind = batch % 2 == 0 ? CommandKind.Hold : CommandKind.Gather;
                    int target = kind == CommandKind.Gather ? SimulationFixture.Id(synchronous, "mineral", -1) : 0;
                    synchronous.QueueCommand(synchronous.MakeCommand(0, kind, new[] { worker }, targetId: target));
                    asynchronous.QueueCommand(asynchronous.MakeCommand(0, kind, new[] { worker }, targetId: target));
                    runner.Schedule(asynchronous, rules, 5);
                    for (int tick = 0; tick < 5; tick++) synchronous.Step();
                    var frame = runner.Complete();
                    Assert.That(frame.CompletedSteps, Is.EqualTo(5));
                    Assert.That(frame.Observation.Tick, Is.EqualTo(synchronous.Tick));
                    Assert.That(asynchronous.StateHash(), Is.EqualTo(synchronous.StateHash()), "Batch " + batch);
                    Assert.That(JsonUtility.ToJson(frame.Observation), Is.EqualTo(JsonUtility.ToJson(synchronous.Observe(0))));
                    Assert.That(frame.OwnCombatStats["marine"].Damage, Is.EqualTo(synchronous.CombatDamage(0, "marine")));
                    Assert.That(frame.OwnCombatStats["marine"].Armor, Is.EqualTo(synchronous.CombatArmor(0, "marine")));
                    Assert.That(frame.ComputeMilliseconds, Is.GreaterThanOrEqualTo(0));
                    if (batch == 9)
                        asynchronous = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(asynchronous.CaptureState())));
                }
                Assert.That(asynchronous.Tick, Is.EqualTo(100));
                Assert.That(JsonUtility.ToJson(asynchronous.ExportReplay()), Is.EqualTo(JsonUtility.ToJson(synchronous.ExportReplay())));
                var replayData = asynchronous.ExportReplay();
                var replay = Match.BeginReplay(rules, replayData);
                runner.Schedule(replay, rules, replayData.FinalTick + 20, replayData.FinalTick);
                var replayFrame = runner.Complete();
                Assert.That(replayFrame.CompletedSteps, Is.EqualTo(100));
                Assert.That(replayFrame.ReplayEnded, Is.True);
                Assert.That(replay.Tick, Is.EqualTo(100));
                Assert.That(replay.StateHash(), Is.EqualTo(synchronous.StateHash()));
            }
        }

        [Test, Timeout(5000)]
        public void ResultsIncludeEveryStepAndDoNotShareTheMatchResultsOrObservation()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            int worker = SimulationFixture.Id(match, "worker");
            var accepted = match.MakeCommand(0, CommandKind.Hold, new[] { worker });
            var rejected = match.MakeCommand(0, CommandKind.Attack, new[] { worker }, targetId: -1);
            rejected.Tick = 1;
            match.QueueCommand(accepted); match.QueueCommand(rejected);
            using (var runner = new SimulationFrameRunner())
            {
                runner.Schedule(match, rules, 2);
                var frame = runner.Complete();
                Assert.That(frame.Results.Select(r => r.Accepted), Is.EqualTo(new[] { true, false }));
                Assert.That(frame.Results[1], Is.Not.SameAs(match.LastResults.Single()));
                frame.Results[1].Reason = "mutated publication";
                Assert.That(match.LastResults.Single().Reason, Is.Not.EqualTo("mutated publication"));
                var before = match.StateHash();
                frame.Observation.Entities.Clear();
                Array.Clear(frame.Observation.Visible, 0, frame.Observation.Visible.Length);
                frame.OwnCombatStats.Clear();
                Assert.That(match.StateHash(), Is.EqualTo(before));
                Assert.That(match.Observe(0).Entities, Is.Not.Empty);
            }
        }

        [Test, Timeout(5000)]
        public void CompletedTaskStillBlocksOverlapUntilPublicationIsConsumed()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            using (var runner = new SimulationFrameRunner())
            {
                var task = runner.Schedule(match, rules, 2);
                task.GetAwaiter().GetResult();
                Assert.That(runner.HasPending, Is.True);
                Assert.Throws<InvalidOperationException>(() => runner.Schedule(match, rules, 1));
                Assert.That(runner.TryComplete(out var frame), Is.True);
                Assert.That(frame.CompletedSteps, Is.EqualTo(2));
                Assert.That(runner.HasPending, Is.False);
                Assert.That(runner.TryComplete(out var absent), Is.False);
                Assert.That(absent, Is.Null);
                Assert.That(runner.Complete(), Is.Null);
            }
        }

        [Test, Timeout(5000)]
        public void DisposeWaitsForPendingStepsAndRejectsFurtherScheduling()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            var runner = new SimulationFrameRunner();
            var task = runner.Schedule(match, rules, 7);
            runner.Dispose();
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(match.Tick, Is.EqualTo(7));
            Assert.That(runner.HasPending, Is.False);
            Assert.Throws<ObjectDisposedException>(() => runner.Schedule(match, rules, 1));
            runner.Dispose();
        }

        [Test, Timeout(5000)]
        public void InvalidRequestsAndWorkerFailuresCannotStrandPendingOwnership()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            using (var runner = new SimulationFrameRunner())
            {
                Assert.Throws<ArgumentNullException>(() => runner.Schedule(null, rules, 1));
                Assert.Throws<ArgumentNullException>(() => runner.Schedule(match, null, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => runner.Schedule(match, rules, -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => runner.Schedule(match, rules, 1, -2));
                Assert.That(runner.HasPending, Is.False);
                // A caller supplying an incompatible public catalog must surface the authoritative
                // query failure, never poison Match internals or leave ownership held forever.
                var incompatible = new RuleSet { Definitions = new[] { new EntityDefinition { Id = "not-in-this-match" } } };
                runner.Schedule(match, incompatible, 0);
                Assert.Throws<ArgumentException>(() => runner.Complete());
                Assert.That(runner.HasPending, Is.False);
                runner.Schedule(match, rules, 1);
                Assert.That(runner.Complete().CompletedSteps, Is.EqualTo(1));
            }
        }

        [Test, Timeout(5000)]
        public void FinishedMatchAndReachedReplayBoundaryNeverAdvancePastTheirEnd()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            using (var runner = new SimulationFrameRunner())
            {
                runner.Schedule(match, rules, 10, 3);
                var frame = runner.Complete();
                Assert.That(frame.CompletedSteps, Is.EqualTo(3));
                Assert.That(frame.ReplayEnded, Is.True);
                runner.Schedule(match, rules, 10, 3);
                Assert.That(runner.Complete().CompletedSteps, Is.Zero);
                var finished = match.CaptureState();
                finished.Phase = MatchPhase.Finished; finished.Winner = 0;
                match = Match.Restore(rules, finished);
                runner.Schedule(match, rules, 10);
                frame = runner.Complete();
                Assert.That(frame.CompletedSteps, Is.Zero);
                Assert.That(frame.Observation.Phase, Is.EqualTo(MatchPhase.Finished));
                Assert.That(frame.ReplayEnded, Is.False);
            }
        }

        [Test, Timeout(5000)]
        public void TerminalOutcomeDuringBatchStopsFurtherTicksAndRetainsItsCommandResult()
        {
            var rules = SimulationFixture.Rules();
            var marine = rules.Definitions.Single(d => d.Id == "marine");
            marine.Damage = 10000; marine.AttackRange = 60000; marine.Sight = 60000;
            var match = new Match(rules, false);
            int attacker = SimulationFixture.Id(match, "marine");
            int target = SimulationFixture.Id(match, "command", 1);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Attack, new[] { attacker }, targetId: target));
            using (var runner = new SimulationFrameRunner())
            {
                runner.Schedule(match, rules, 10);
                var frame = runner.Complete();
                Assert.That(frame.CompletedSteps, Is.EqualTo(1));
                Assert.That(frame.Observation.Tick, Is.EqualTo(1));
                Assert.That(frame.Observation.Phase, Is.EqualTo(MatchPhase.Finished));
                Assert.That(frame.Observation.Winner, Is.EqualTo(0));
                Assert.That(frame.Results.Single().Accepted, Is.True);
            }
        }

        [Test, Timeout(5000)]
        public void NonblockingCompletionAndDisposeAlsoReleaseFaultedWorkerOwnership()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, false);
            var incompatible = new RuleSet { Definitions = new[] { new EntityDefinition { Id = "not-in-this-match" } } };
            var runner = new SimulationFrameRunner();
            var failed = runner.Schedule(match, incompatible, 0);
            Assert.Throws<ArgumentException>(() => failed.GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => runner.TryComplete(out _));
            Assert.That(runner.HasPending, Is.False);
            runner.Schedule(match, incompatible, 0);
            Assert.Throws<ArgumentException>(() => runner.Dispose());
            Assert.That(runner.HasPending, Is.False);
            Assert.Throws<ObjectDisposedException>(() => runner.Schedule(match, rules, 0));
            runner.Dispose();
        }

        [Test, Timeout(5000)]
        public void ResearchProjectionUsesTheAuthoritativeGateForOwnedAndUnfinishedProducers()
        {
            var rules = SimulationFixture.Rules();
            rules.Upgrades = new[] { new UpgradeDefinition { Id = "weapons", ProducerId = "barracks",
                MineralCost = 10, ResearchTicks = 20, DamageBonus = 1, AffectedDefinitionIds = new[] { "marine" } } };
            var match = new Match(rules, false);
            int producer = SimulationFixture.Id(match, "barracks");
            using (var runner = new SimulationFrameRunner())
            {
                runner.Schedule(match, rules, 0);
                var frame = runner.Complete();
                Assert.That(frame.ResearchAvailability.Keys, Is.EqualTo(new[] { producer }));
                Assert.That(frame.ResearchAvailability[producer]["weapons"].Accepted, Is.True);
                Assert.That(frame.MissingProducerAvailability["weapons"].Reason,
                    Is.EqualTo(match.ResearchAvailability(0, 0, "weapons").Reason));
                var saved = match.CaptureState();
                saved.Entities.Single(e => e.Id == producer).Completed = false;
                match = Match.Restore(rules, saved);
                runner.Schedule(match, rules, 0);
                frame = runner.Complete();
                Assert.That(frame.ResearchAvailability[producer]["weapons"].Accepted, Is.False);
                Assert.That(frame.ResearchAvailability[producer]["weapons"].Reason,
                    Is.EqualTo(match.ResearchAvailability(0, producer, "weapons").Reason));
                frame.ResearchAvailability[producer]["weapons"].Reason = "changed publication";
                Assert.That(match.ResearchAvailability(0, producer, "weapons").Reason, Is.Not.EqualTo("changed publication"));
            }
        }
    }
}
