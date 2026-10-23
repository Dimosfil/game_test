using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class CombatTargetingTests
    {
        static SpawnDefinition Spawn(string id, int owner, int x, int z) =>
            new SpawnDefinition { DefinitionId = id, Owner = owner, Position = new SimPoint(x, z) };

        static RuleSet Rules(bool alliedVision = false) => new RuleSet
        {
            VersionId = "qa-combat-targeting-v1", TickRate = 20, MapWidth = 40, MapHeight = 32,
            GatherTicks = 1, CargoAmount = 1, Blocks = new MapBlock[0],
            Definitions = new[]
            {
                new EntityDefinition { Id = "base", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                    Radius = 1000, BuildTicks = 1 },
                new EntityDefinition { Id = "shooter", MaxHealth = 100, Radius = 300, Speed = 4000,
                    Damage = 2, AttackCooldown = 10, AttackRange = 6000, Sight = alliedVision ? 1000 : 6000, BuildTicks = 1 },
                new EntityDefinition { Id = "dummy", MaxHealth = 100, Radius = 300, BuildTicks = 1 },
                new EntityDefinition { Id = "scout", MaxHealth = 100, Radius = 300, Sight = 2500, BuildTicks = 1 }
            },
            Spawns = alliedVision ? new[]
            {
                Spawn("base", 0, 3000, 3000), Spawn("base", 1, 36000, 28000), Spawn("shooter", 0, 10000, 10000),
                Spawn("dummy", 1, 11500, 10000), Spawn("dummy", 1, 14000, 10000), Spawn("scout", 0, 14000, 12000)
            } : new[]
            {
                Spawn("base", 0, 3000, 3000), Spawn("base", 1, 36000, 28000), Spawn("shooter", 0, 10000, 10000),
                Spawn("dummy", 1, 12000, 10000), Spawn("dummy", 1, 10000, 12000)
            }
        };

        [Test]
        public void EqualDistanceTargetsChooseLowestIdEvenWithReversedEntityIterationOrder()
        {
            var rules = Rules();
            var initial = new Match(rules, false);
            var saved = initial.CaptureState();
            int shooter = saved.Entities.Single(e => e.DefinitionId == "shooter").Id;
            var targets = saved.Entities.Where(e => e.DefinitionId == "dummy").OrderBy(e => e.Id).ToArray();
            saved.Entities.Reverse();
            var match = Match.Restore(rules, saved);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Hold, new[] { shooter }));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            Assert.That(match.Find(shooter).LastAttackTarget, Is.EqualTo(targets[0].Id));
            Assert.That(match.Find(targets[0].Id).Health, Is.EqualTo(98));
            Assert.That(match.Find(targets[1].Id).Health, Is.EqualTo(100));
        }

        [Test]
        public void HoldSkipsCloserHiddenEnemyAndFiresAtFartherAllyVisibleEnemy()
        {
            var match = new Match(Rules(true), false);
            var state = match.CaptureState();
            int shooter = state.Entities.Single(e => e.DefinitionId == "shooter").Id;
            var targets = state.Entities.Where(e => e.DefinitionId == "dummy").OrderBy(e => e.Position.X).ToArray();
            Assert.That(match.Observe(0).Entities.Any(e => e.Id == targets[0].Id), Is.False);
            Assert.That(match.Observe(0).Entities.Any(e => e.Id == targets[1].Id), Is.True);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Hold, new[] { shooter }));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            Assert.That(match.Find(shooter).LastAttackTarget, Is.EqualTo(targets[1].Id));
            Assert.That(match.Find(targets[0].Id).Health, Is.EqualTo(100));
            Assert.That(match.Find(targets[1].Id).Health, Is.EqualTo(98));
        }

        [Test]
        public void AttackMoveDoesNotAcquireAllyVisibleEnemyOutsideAttackerChaseSight()
        {
            var match = new Match(Rules(true), false);
            int shooter = match.Observe(0).Entities.Single(e => e.DefinitionId == "shooter").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.AttackMove, new[] { shooter }, new SimPoint(11000, 10000)));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            Assert.That(match.Find(shooter).LastAttackTarget, Is.Zero);
            Assert.That(match.Find(shooter).Position.X, Is.GreaterThan(10000), "No valid local target must allow the positional order to continue.");
            Assert.That(match.CaptureState().Entities.Where(e => e.DefinitionId == "dummy").All(e => e.Health == 100), Is.True);
        }

        [Test]
        public void LaterShooterIgnoresTargetKilledEarlierInTheSameTick()
        {
            var rules = Rules();
            rules.Spawns = rules.Spawns.Concat(new[] { Spawn("shooter", 0, 10000, 12000) }).ToArray();
            var initial = new Match(rules, false);
            var state = initial.CaptureState();
            var shooters = state.Entities.Where(e => e.DefinitionId == "shooter").OrderBy(e => e.Id).ToArray();
            var targets = state.Entities.Where(e => e.DefinitionId == "dummy").OrderBy(e => e.Id).ToArray();
            targets[0].Health = 2;
            targets[1].Position = new SimPoint(14000, 10000);
            var match = Match.Restore(rules, state);
            match.Step();
            Assert.That(match.Find(targets[0].Id), Is.Null);
            Assert.That(match.Find(shooters[0].Id).LastAttackTarget, Is.EqualTo(targets[0].Id));
            Assert.That(match.Find(shooters[1].Id).LastAttackTarget, Is.EqualTo(targets[1].Id));
            Assert.That(match.Find(targets[1].Id).Health, Is.EqualTo(98));
        }

        [Test]
        public void EnemyProducedAfterPreviousTickIndexIsTargetableOnItsSpawnTick()
        {
            var rules = Rules();
            rules.Definitions.Single(d => d.Id == "shooter").Sight = 7000;
            rules.Definitions.Single(d => d.Id == "shooter").AttackRange = 9000;
            var dummy = rules.Definitions.Single(d => d.Id == "dummy");
            dummy.ProducerId = "base";
            dummy.BuildTicks = 1;
            rules.Spawns = rules.Spawns.Where(s => s.DefinitionId != "dummy").ToArray();
            rules.Spawns.Single(s => s.Owner == 1 && s.DefinitionId == "base").Position = new SimPoint(2000, 10000);
            var match = new Match(rules, false);
            int shooter = match.Observe(0).Entities.Single(e => e.DefinitionId == "shooter").Id;
            int producer = match.Observe(1).Entities.Single(e => e.Owner == 1 && e.DefinitionId == "base").Id;
            match.Step();
            Assert.That(match.Find(shooter).LastAttackTarget, Is.Zero, "The producer is beyond all allied sight.");
            var restored = Match.Restore(rules, match.CaptureState());
            foreach (var timeline in new[] { match, restored })
            {
                timeline.QueueCommand(timeline.MakeCommand(1, CommandKind.Train, new[] { producer }, definitionId: "dummy"));
                timeline.Step();
                Assert.That(timeline.LastResults.Single().Accepted, Is.True);
                var created = timeline.CaptureState().Entities.Single(e => e.DefinitionId == "dummy");
                Assert.That(timeline.Find(shooter).LastAttackTarget, Is.EqualTo(created.Id));
                Assert.That(created.Health, Is.EqualTo(98));
            }
            Assert.That(restored.StateHash(), Is.EqualTo(match.StateHash()));
        }

        [TestCase(CommandKind.Hold, 1000, true)]
        [TestCase(CommandKind.Hold, 1001, false)]
        [TestCase(CommandKind.AttackMove, 1000, true)]
        [TestCase(CommandKind.AttackMove, 1001, false)]
        public void SelfSightWitnessKeepsExactInclusiveBoundary(CommandKind kind, int distance, bool visible)
        {
            var rules = Rules(true);
            rules.Spawns = rules.Spawns.Where(s => s.DefinitionId != "scout").ToArray();
            var firstTarget = rules.Spawns.First(s => s.DefinitionId == "dummy");
            firstTarget.Position = new SimPoint(10000 + distance, 10000);
            var match = new Match(rules, false);
            int shooter = match.Observe(0).Entities.Single(e => e.DefinitionId == "shooter").Id;
            int target = match.CaptureState().Entities.First(e => e.DefinitionId == "dummy").Id;
            Assert.That(match.Observe(0).Entities.Any(e => e.Id == target), Is.EqualTo(visible));
            match.QueueCommand(match.MakeCommand(0, kind, new[] { shooter }, new SimPoint(11000, 10000)));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            Assert.That(match.Find(target).Health, Is.EqualTo(visible ? 98 : 100));
            Assert.That(match.Find(shooter).LastAttackTarget, Is.EqualTo(visible ? target : 0));
        }

        [Test]
        public void ProducedShooterActsOnItsBirthTickWithoutAdvancingTheNextProductionItem()
        {
            var rules = Rules();
            var definition = rules.Definitions.Single(d => d.Id == "shooter");
            definition.ProducerId = "base";
            definition.BuildTicks = 1;
            rules.Spawns = rules.Spawns.Where(s => s.DefinitionId != "shooter").ToArray();
            rules.Spawns.Single(s => s.Owner == 0 && s.DefinitionId == "base").Position = new SimPoint(10000, 10000);
            rules.Spawns.First(s => s.DefinitionId == "dummy").Position = new SimPoint(14000, 10000);
            var match = new Match(rules, false);
            int producer = match.Observe(0).Entities.Single(e => e.Owner == 0 && e.DefinitionId == "base").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.Train, new[] { producer }, definitionId: "shooter"));
            match.QueueCommand(match.MakeCommand(0, CommandKind.Train, new[] { producer }, definitionId: "shooter"));
            match.Step();
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
            var created = match.CaptureState().Entities.Single(e => e.DefinitionId == "shooter");
            Assert.That(created.LastAttackTick, Is.Zero, "The order phase must include the producer's appended newborn immediately.");
            Assert.That(match.Find(created.LastAttackTarget).Health, Is.EqualTo(98));
            Assert.That(match.Find(producer).Production.Count, Is.EqualTo(1));
            Assert.That(match.Find(producer).Production[0].RemainingTicks, Is.EqualTo(1), "Production handles one phase-entry producer turn.");
            Assert.That(match.CaptureCombatDiagnostics().Shots0, Is.EqualTo(1));
        }
    }
}
