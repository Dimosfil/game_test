using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    public sealed class NavigationWorkspaceTests
    {
        static RuleSet Rules(bool divided = false) => new RuleSet
        {
            VersionId = "qa-navigation-workspace-v1", TickRate = 20, MapWidth = 40, MapHeight = 32,
            GatherTicks = 1, CargoAmount = 1,
            Definitions = new[]
            {
                new EntityDefinition { Id = "base", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                    Radius = 1000, SupplyProvided = 100, BuildTicks = 1 },
                new EntityDefinition { Id = "runner", MaxHealth = 40, Radius = 350, Speed = 4000, SupplyCost = 1, BuildTicks = 1 }
            },
            Blocks = divided ? new[] { new MapBlock { X = 20, Z = 0, Width = 2, Height = 32 },
                new MapBlock { X = 10, Z = 8, Width = 1, Height = 16 } } :
                new[] { new MapBlock { X = 10, Z = 10, Width = 1, Height = 14 } },
            Spawns = new[]
            {
                new SpawnDefinition { DefinitionId = "base", Owner = 0, Position = new SimPoint(3000, 3000) },
                new SpawnDefinition { DefinitionId = "base", Owner = 1, Position = new SimPoint(36000, 28000) },
                new SpawnDefinition { DefinitionId = "runner", Owner = 0, Position = new SimPoint(6000, divided ? 4000 : 14000) },
                new SpawnDefinition { DefinitionId = "runner", Owner = 0, Position = new SimPoint(6000, 20000) }
            }
        };

        [Test, Timeout(15000)]
        public void DistinctSameTickRoutesRemainIndependentAndRestoreContinuesExactly()
        {
            var rules = Rules();
            var match = new Match(rules, false);
            var ids = match.Observe(0).Entities.Where(e => e.DefinitionId == "runner").Select(e => e.Id).ToArray();
            var goals = new[] { new SimPoint(16000, 14000), new SimPoint(16000, 20000) };
            for (int index = 0; index < ids.Length; index++)
                match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { ids[index] }, goals[index]));
            match.Step();
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
            for (int index = 0; index < ids.Length; index++)
            {
                var path = match.Find(ids[index]).Path;
                Assert.That(path.Count, Is.GreaterThan(2), "Each route must use the obstacle detour, not a direct LOS shortcut.");
                Assert.That(path.Last().X, Is.EqualTo(goals[index].X));
                Assert.That(path.Last().Z, Is.EqualTo(goals[index].Z));
            }
            for (int tick = 0; tick < 24; tick++) match.Step();
            var restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(match.CaptureState())));
            for (int tick = 0; tick < 400 && ids.Any(id => match.Find(id).Orders.Count > 0); tick++)
            {
                match.Step(); restored.Step();
                Assert.That(JsonUtility.ToJson(restored.CaptureState()), Is.EqualTo(JsonUtility.ToJson(match.CaptureState())), "Route continuation tick " + match.Tick);
            }
            for (int index = 0; index < ids.Length; index++)
            {
                var entity = match.Find(ids[index]);
                Assert.That(entity.Orders, Is.Empty);
                Assert.That(entity.Position.X, Is.EqualTo(goals[index].X));
                Assert.That(entity.Position.Z, Is.EqualTo(goals[index].Z));
            }
        }

        [Test, Timeout(15000)]
        public void UnreachableSearchCannotPoisonTheFollowingActorsReachableDetour()
        {
            var match = new Match(Rules(true), false);
            var ids = match.Observe(0).Entities.Where(e => e.DefinitionId == "runner").Select(e => e.Id).ToArray();
            var goal = new SimPoint(16000, 20000);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { ids[0] }, new SimPoint(30000, 4000)));
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { ids[1] }, goal));
            match.Step();
            Assert.That(match.Find(ids[0]).Path, Is.Empty, "The full-height divider makes this route unreachable.");
            Assert.That(match.Find(ids[1]).Path.Count, Is.GreaterThan(2), "A later search must clear the exhausted frontier and closed nodes.");
            match.QueueCommand(match.MakeCommand(0, CommandKind.Stop, new[] { ids[0] }));
            for (int tick = 0; tick < 300 && match.Find(ids[1]).Orders.Count > 0; tick++) match.Step();
            Assert.That(match.Find(ids[1]).Orders, Is.Empty);
            Assert.That(match.Find(ids[1]).Position.X, Is.EqualTo(goal.X));
            Assert.That(match.Find(ids[1]).Position.Z, Is.EqualTo(goal.Z));
        }

        [TestCase(16999, 6141, 16859)]
        [TestCase(17000, 6141, 16859)]
        [TestCase(17001, 6189, 17063)]
        public void ChaseGoalMidpointTieRetainsTheEstablishedSideOfTheWall(int targetZ, int firstX, int firstZ)
        {
            var rules = Rules();
            var actorDefinition = rules.Definitions.Single(d => d.Id == "runner");
            actorDefinition.Damage = 1; actorDefinition.AttackCooldown = 3; actorDefinition.Sight = 40000;
            rules.Definitions = rules.Definitions.Concat(new[] { new EntityDefinition { Id = "target", MaxHealth = 100,
                Radius = 100, BuildTicks = 1 } }).ToArray();
            rules.Spawns[2].Position = new SimPoint(6000, 17000);
            rules.Spawns[3] = new SpawnDefinition { DefinitionId = "target", Owner = 1, Position = new SimPoint(16000, targetZ) };
            var match = new Match(rules, false);
            var actor = match.Observe(0).Entities.Single(e => e.DefinitionId == "runner");
            int target = match.Observe(0).Entities.Single(e => e.DefinitionId == "target").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.Attack, new[] { actor.Id }, targetId: target));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            var moved = match.Find(actor.Id);
            Assert.That(moved.Path.Count, Is.GreaterThan(2), "Chase must exercise the A* stopping-distance route around the wall.");
            // Public movement goldens from the original exhaustive goal scan: one millimetre
            // across the midpoint changes the detour, while an exact tie retains the lower row.
            Assert.That(moved.Position.X, Is.EqualTo(firstX));
            Assert.That(moved.Position.Z, Is.EqualTo(firstZ));
        }

        [Test, Timeout(15000)]
        public void BlockedNearestGoalFallsBackAndChaserReachesItsFiringRange()
        {
            var rules = Rules();
            var actorDefinition = rules.Definitions.Single(d => d.Id == "runner");
            actorDefinition.Damage = 1; actorDefinition.AttackCooldown = 3;
            actorDefinition.AttackRange = 3000; actorDefinition.Sight = 40000;
            rules.Spawns = rules.Spawns.Take(3).ToArray();
            rules.Spawns[2].Position = new SimPoint(26000, 28000);
            var match = new Match(rules, false);
            var actor = match.Observe(0).Entities.Single(e => e.DefinitionId == "runner");
            int target = match.Observe(0).Entities.Single(e => e.Owner == 1 && e.DefinitionId == "base").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.Attack, new[] { actor.Id }, targetId: target));
            for (int tick = 0; tick < 120 && match.Find(actor.Id).LastAttackTick < 0; tick++) match.Step();
            Assert.That(match.Find(actor.Id).LastAttackTarget, Is.EqualTo(target));
            Assert.That(match.Find(target).Health, Is.LessThan(1000), "A building occupies its nearest grid centres; selecting only that blocked cell must not strand the chaser.");
        }

        [TestCase(350)]
        [TestCase(39649)]
        public void ChaseCanApproachTargetsAtEitherHorizontalMapEdge(int targetX)
        {
            var rules = Rules();
            var actorDefinition = rules.Definitions.Single(d => d.Id == "runner");
            actorDefinition.Damage = 1; actorDefinition.AttackCooldown = 3;
            actorDefinition.AttackRange = 1000; actorDefinition.Sight = 40000;
            rules.Definitions = rules.Definitions.Concat(new[] { new EntityDefinition { Id = "target", MaxHealth = 100,
                Radius = 100, BuildTicks = 1 } }).ToArray();
            rules.Spawns[2].Position = new SimPoint(6000, 500);
            rules.Spawns[3] = new SpawnDefinition { DefinitionId = "target", Owner = 1, Position = new SimPoint(targetX, 350) };
            var match = new Match(rules, false);
            int actor = match.Observe(0).Entities.Single(e => e.DefinitionId == "runner").Id;
            int target = match.Observe(0).Entities.Single(e => e.DefinitionId == "target").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.Attack, new[] { actor }, targetId: target));
            for (int tick = 0; tick < 250 && match.Find(actor).LastAttackTick < 0; tick++) match.Step();
            var entity = match.Find(actor);
            Assert.That(entity.LastAttackTarget, Is.EqualTo(target));
            Assert.That(entity.Position.X, Is.InRange(350, 39649));
            Assert.That(entity.Position.Z, Is.InRange(350, 31649));
        }
    }
}
