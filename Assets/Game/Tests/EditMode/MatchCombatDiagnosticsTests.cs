using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class MatchCombatDiagnosticsTests
    {
        static RuleSet Rules() => new RuleSet
        {
            VersionId = "qa-combat-diagnostics-v1", TickRate = 20, MapWidth = 40, MapHeight = 32,
            GatherTicks = 1, CargoAmount = 1, Blocks = new MapBlock[0],
            Definitions = new[]
            {
                new EntityDefinition { Id = "base", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                    Radius = 1000, BuildTicks = 1 },
                new EntityDefinition { Id = "marine", MaxHealth = 45, Radius = 350, Speed = 4000,
                    Damage = 6, AttackCooldown = 3, AttackRange = 6000, Sight = 6000, BuildTicks = 1 }
            },
            Spawns = new[]
            {
                new SpawnDefinition { DefinitionId = "base", Owner = 0, Position = new SimPoint(3000, 3000) },
                new SpawnDefinition { DefinitionId = "base", Owner = 1, Position = new SimPoint(36000, 28000) },
                new SpawnDefinition { DefinitionId = "marine", Owner = 0, Position = new SimPoint(10000, 10000) },
                new SpawnDefinition { DefinitionId = "marine", Owner = 1, Position = new SimPoint(12000, 10000) }
            }
        };

        [Test]
        public void CountersTrackActualShotsAndHealthRatherThanCooldownTargetQueries()
        {
            var match = new Match(Rules(), false);
            var initial = match.CaptureCombatDiagnostics();
            Assert.That(initial.Shots0 + initial.Shots1, Is.Zero);
            Assert.That(initial.LiveMarines0, Is.EqualTo(1));
            Assert.That(initial.MarineHealth0, Is.EqualTo(45));
            match.Step();
            var first = match.CaptureCombatDiagnostics();
            Assert.That(first.Shots0, Is.EqualTo(1));
            Assert.That(first.Shots1, Is.EqualTo(1));
            Assert.That(first.HealthDamage0, Is.EqualTo(6));
            Assert.That(first.HealthDamage1, Is.EqualTo(6));
            Assert.That(first.MarineHealth0, Is.EqualTo(39));
            Assert.That(first.MarineHealth1, Is.EqualTo(39));
            match.Step(); match.Step();
            Assert.That(match.CaptureCombatDiagnostics().Shots0, Is.EqualTo(1));
            match.Step();
            Assert.That(match.CaptureCombatDiagnostics().Shots0, Is.EqualTo(2));
            Assert.That(match.CaptureCombatDiagnostics().MarineHealth1, Is.EqualTo(33));
        }

        [Test]
        public void OverkillCreditsOnlyRemainingHealthAndDeathBelongsToTheVictimOwner()
        {
            var rules = Rules();
            rules.Definitions.Single(d => d.Id == "marine").Damage = 100;
            var match = new Match(rules, false);
            match.Step();
            var stats = match.CaptureCombatDiagnostics();
            Assert.That(stats.Shots0, Is.EqualTo(1));
            Assert.That(stats.Shots1, Is.Zero, "The victim died before its turn.");
            Assert.That(stats.HealthDamage0, Is.EqualTo(45));
            Assert.That(stats.HealthDamage1, Is.Zero);
            Assert.That(stats.Deaths0, Is.Zero);
            Assert.That(stats.Deaths1, Is.EqualTo(1));
            Assert.That(stats.LiveMarines0, Is.EqualTo(1));
            Assert.That(stats.LiveMarines1, Is.Zero);
            Assert.That(stats.MarineHealth1, Is.Zero);
            match.Step();
            Assert.That(match.CaptureCombatDiagnostics().Deaths1, Is.EqualTo(1));
        }

        [Test]
        public void ChasingAVisibleTargetDoesNotRecordAnUnfiredShot()
        {
            var rules = Rules();
            rules.Definitions.Single(d => d.Id == "marine").Sight = 12000;
            rules.Spawns.Single(s => s.DefinitionId == "marine" && s.Owner == 1).Position = new SimPoint(20000, 10000);
            var match = new Match(rules, false);
            int attacker = match.Observe(0).Entities.Single(e => e.Owner == 0 && e.DefinitionId == "marine").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.AttackMove, new[] { attacker }, new SimPoint(22000, 10000)));
            match.Step();
            Assert.That(match.Find(attacker).Position.X, Is.GreaterThan(10000));
            Assert.That(match.CaptureCombatDiagnostics().Shots0 + match.CaptureCombatDiagnostics().Shots1, Is.Zero);
            Assert.That(match.CaptureCombatDiagnostics().MarineHealth1, Is.EqualTo(45));
        }

        [Test]
        public void RepeatedDiagnosticCaptureAndMutationDoNotChangeSnapshotOrGameplay()
        {
            var match = new Match(Rules(), false);
            match.Step();
            var hash = match.StateHash();
            string saved = JsonUtility.ToJson(match.CaptureState());
            var detached = match.CaptureCombatDiagnostics();
            detached.Shots0 = 123456;
            detached.MarineHealth0 = 0;
            Assert.That(match.CaptureCombatDiagnostics().Shots0, Is.EqualTo(1));
            Assert.That(match.StateHash(), Is.EqualTo(hash));
            Assert.That(JsonUtility.ToJson(match.CaptureState()), Is.EqualTo(saved));
        }

        [Test]
        public void RestoreResetsDerivedTotalsWhileReplayRebuildsTheSameEvents()
        {
            var rules = Rules();
            var original = new Match(rules, false);
            original.Step();
            var restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(original.CaptureState())));
            Assert.That(restored.StateHash(), Is.EqualTo(original.StateHash()));
            Assert.That(restored.CaptureCombatDiagnostics().Shots0, Is.Zero);
            Assert.That(restored.CaptureCombatDiagnostics().HealthDamage0, Is.Zero);
            Assert.That(restored.CaptureCombatDiagnostics().MarineHealth0, Is.EqualTo(original.CaptureCombatDiagnostics().MarineHealth0));
            for (int i = 0; i < 3; i++) { original.Step(); restored.Step(); }
            Assert.That(restored.StateHash(), Is.EqualTo(original.StateHash()));
            Assert.That(original.CaptureCombatDiagnostics().Shots0, Is.EqualTo(2));
            Assert.That(restored.CaptureCombatDiagnostics().Shots0, Is.EqualTo(1));
            var replay = Match.FromReplay(rules, original.ExportReplay());
            Assert.That(replay.StateHash(), Is.EqualTo(original.StateHash()));
            Assert.That(replay.CaptureCombatDiagnostics().Shots0, Is.EqualTo(original.CaptureCombatDiagnostics().Shots0));
            Assert.That(replay.CaptureCombatDiagnostics().HealthDamage1, Is.EqualTo(original.CaptureCombatDiagnostics().HealthDamage1));
        }

        [Test]
        public void DetailedMovementCountsActualTravelAndDistinctUnitsWithoutChangingGameplay()
        {
            var rules = Rules();
            rules.Spawns.Single(s => s.DefinitionId == "marine" && s.Owner == 1).Position = new SimPoint(30000, 24000);
            var measured = new Match(rules, false) { DetailedDiagnosticsEnabled = true };
            var control = new Match(rules, false);
            int id = measured.Observe(0).Entities.Single(e => e.Owner == 0 && e.DefinitionId == "marine").Id;
            foreach (var match in new[] { measured, control })
                match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { id }, new SimPoint(10800, 10000)));
            long travel = 0;
            for (int tick = 0; tick < 6; tick++)
            {
                var before = measured.Find(id).Position;
                measured.Step(); control.Step();
                var after = measured.Find(id).Position;
                int distance = System.Math.Abs(after.X - before.X);
                Assert.That(after.Z, Is.EqualTo(before.Z));
                travel += distance;
                var stats = measured.CaptureCombatDiagnostics();
                Assert.That(stats.MarineTravel0, Is.EqualTo(travel));
                Assert.That(stats.MovedMarines0, Is.EqualTo(1));
                Assert.That(stats.TickMovingMarines0, Is.EqualTo(distance > 0 ? 1 : 0));
                Assert.That(stats.MovedMarines1, Is.Zero);
                Assert.That(control.CaptureCombatDiagnostics().MarineTravel0, Is.Zero);
                Assert.That(JsonUtility.ToJson(measured.CaptureState()), Is.EqualTo(JsonUtility.ToJson(control.CaptureState())));
                Assert.That(JsonUtility.ToJson(measured.Observe(0)), Is.EqualTo(JsonUtility.ToJson(control.Observe(0))));
                Assert.That(measured.StateHash(), Is.EqualTo(control.StateHash()));
            }
            measured.DetailedDiagnosticsEnabled = false;
            measured.QueueCommand(measured.MakeCommand(0, CommandKind.Move, new[] { id }, new SimPoint(12000, 10000)));
            measured.Step();
            Assert.That(measured.CaptureCombatDiagnostics().MarineTravel0, Is.EqualTo(travel));
            Assert.That(measured.CaptureCombatDiagnostics().TickMovingMarines0, Is.Zero);
        }

        [TestCase(0, 1, 2)]
        [TestCase(1, 0, 1)]
        public void DetailedBotCountersMeasureThinkingAndAcceptedCommandsRatherThanAttempts(int trainCost, int trains, int accepted)
        {
            var rules = Rules();
            var marine = rules.Definitions.Single(d => d.Id == "marine");
            marine.ProducerId = "base"; marine.MineralCost = trainCost;
            rules.Spawns = rules.Spawns.Concat(new[]
            {
                new SpawnDefinition { DefinitionId = "marine", Owner = 1, Position = new SimPoint(14000, 10000) },
                new SpawnDefinition { DefinitionId = "marine", Owner = 1, Position = new SimPoint(16000, 10000) },
                new SpawnDefinition { DefinitionId = "marine", Owner = 1, Position = new SimPoint(18000, 10000) }
            }).ToArray();
            var measured = new Match(rules, true) { DetailedDiagnosticsEnabled = true };
            var control = new Match(rules, true);
            int own = measured.Observe(0).Entities.Single(e => e.Owner == 0 && e.DefinitionId == "marine").Id;
            foreach (var match in new[] { measured, control }) match.QueueCommand(match.MakeCommand(0, CommandKind.Hold, new[] { own }));
            measured.Step(); control.Step();
            var stats = measured.CaptureCombatDiagnostics();
            Assert.That(stats.BotThinkCount, Is.EqualTo(1));
            Assert.That(stats.BotAcceptedCommands, Is.EqualTo(accepted));
            Assert.That(stats.BotTrainCommands, Is.EqualTo(trains));
            Assert.That(stats.BotAttackMoveCommands, Is.EqualTo(1));
            Assert.That(stats.BotAttackMoveUnits, Is.EqualTo(4));
            Assert.That(stats.BotBuildCommands + stats.BotGatherCommands, Is.Zero);
            Assert.That(measured.LastResults.Count(r => r.Player == 1 && r.Accepted), Is.EqualTo(accepted));
            Assert.That(JsonUtility.ToJson(measured.CaptureState()), Is.EqualTo(JsonUtility.ToJson(control.CaptureState())));
            measured.Step();
            Assert.That(measured.CaptureCombatDiagnostics().BotThinkCount, Is.EqualTo(1));
            var restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(measured.CaptureState())));
            Assert.That(restored.DetailedDiagnosticsEnabled, Is.False);
            Assert.That(restored.CaptureCombatDiagnostics().BotThinkCount, Is.Zero);
            Assert.That(restored.CaptureCombatDiagnostics().BotAcceptedCommands, Is.Zero);
            Assert.That(restored.CaptureCombatDiagnostics().MarineTravel1, Is.Zero);
            Assert.That(restored.StateHash(), Is.EqualTo(measured.StateHash()));
        }
    }
}
