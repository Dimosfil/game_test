using System;
using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class VisibilityCacheTests
    {
        static SpawnDefinition Spawn(string id, int owner, int x, int z) =>
            new SpawnDefinition { DefinitionId = id, Owner = owner, Position = new SimPoint(x, z) };

        static RuleSet Rules() => new RuleSet
        {
            VersionId = "qa-visibility-cache-v1", TickRate = 20, MapWidth = 40, MapHeight = 32,
            StartingMinerals = 1000, GatherTicks = 1, CargoAmount = 1,
            Definitions = new[]
            {
                new EntityDefinition { Id = "command", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                    Radius = 1000, Sight = 0, SupplyProvided = 100, BuildTicks = 1 },
                new EntityDefinition { Id = "worker", IsWorker = true, MaxHealth = 30, Radius = 350,
                    Speed = 4000, Sight = 5500, BuildTicks = 3, ProducerId = "command", MineralCost = 10 },
                new EntityDefinition { Id = "scout", MaxHealth = 20, Radius = 350, Speed = 4000,
                    Sight = 6500, BuildTicks = 3, ProducerId = "command", MineralCost = 10 },
                new EntityDefinition { Id = "raider", MaxHealth = 20, Radius = 350, Speed = 4000,
                    Sight = 10000, Damage = 40, AttackRange = 6000, AttackCooldown = 1, BuildTicks = 1 },
                new EntityDefinition { Id = "tower", IsBuilding = true, MaxHealth = 100, Radius = 400,
                    Sight = 9000, BuildTicks = 3, MineralCost = 20 }
            },
            Spawns = new[] { Spawn("command", 0, 3000, 3000), Spawn("command", 1, 36000, 28000),
                Spawn("worker", 0, 8137, 8731), Spawn("scout", 1, 31413, 22123) },
            Blocks = new[] { new MapBlock { X = 20, Z = 0, Width = 1, Height = 32 } }
        };

        // Independent old per-cell oracle. Entity-centre visibility intentionally differs from cell fog.
        static bool[] Naive(RuleSet rules, MatchState state, int player)
        {
            var result = new bool[rules.MapWidth * rules.MapHeight];
            foreach (var e in state.Entities)
            {
                if (e.Owner != player || e.Health <= 0 || !e.Completed) continue;
                int sight = rules.Definitions.Single(d => d.Id == e.DefinitionId).Sight;
                int x0 = Math.Max(0, (e.Position.X - sight) / 1000), x1 = Math.Min(rules.MapWidth - 1, (e.Position.X + sight) / 1000);
                int z0 = Math.Max(0, (e.Position.Z - sight) / 1000), z1 = Math.Min(rules.MapHeight - 1, (e.Position.Z + sight) / 1000);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    long dx = x * 1000L + 500 - e.Position.X, dz = z * 1000L + 500 - e.Position.Z;
                    if (dx * dx + dz * dz <= (long)sight * sight) result[z * rules.MapWidth + x] = true;
                }
            }
            return result;
        }

        static void AssertMasks(Match match, RuleSet rules)
        {
            var state = match.CaptureState();
            for (int player = 0; player < 2; player++)
                CollectionAssert.AreEqual(Naive(rules, state, player), match.Observe(player).Visible,
                    "Old-raster mismatch at tick " + match.Tick + " player " + player);
        }

        static int Id(Match match, string definition, int player = 0) => match.Observe(player).Entities
            .First(e => e.Owner == player && e.DefinitionId == definition).Id;

        [TestCase(13), TestCase(701), TestCase(12011)]
        public void FractionalCentresEdgesAndInclusiveCircleBoundariesMatchOldRaster(int seed)
        {
            var random = new System.Random(seed);
            int[] sights = { 0, 1, 499, 500, 501, 999, 1000, 1001, 1414, 1415, 5500, int.MaxValue };
            for (int sample = 0; sample < 36; sample++)
            {
                var rules = Rules();
                rules.Definitions.Single(d => d.Id == "worker").Sight = sights[sample % sights.Length];
                var viewer = rules.Spawns.Single(s => s.DefinitionId == "worker");
                viewer.Position = sample < 4 ? new SimPoint(sample % 2 == 0 ? 0 : 39999, sample < 2 ? 0 : 31999) :
                    sample < 8 ? new SimPoint(500 + (sample - 4) * 1000, 500) :
                    new SimPoint(random.Next(40000), random.Next(32000));
                AssertMasks(new Match(rules, false, seed), rules);
            }
        }

        [Test]
        public void RepeatedObservationsCannotMutateCachedVisibilityExplorationOrPlacement()
        {
            var rules = Rules();
            var match = new Match(rules, false);
            var before = match.Observe(0);
            var expectedVisible = (bool[])before.Visible.Clone();
            var expectedExplored = (bool[])before.Explored.Clone();
            bool place = match.CanPlace("tower", new SimPoint(11000, 8000), 0);
            Array.Fill(before.Visible, true);
            Array.Fill(before.Explored, false);
            var after = match.Observe(0);
            CollectionAssert.AreEqual(expectedVisible, after.Visible);
            CollectionAssert.AreEqual(expectedExplored, after.Explored);
            Assert.That(match.CanPlace("tower", new SimPoint(11000, 8000), 0), Is.EqualTo(place));
            Assert.That(match.CanPlace("tower", new SimPoint(30000, 6000), 0), Is.False);
            AssertMasks(match, rules);
        }

        [Test]
        public void MovingViewerRefreshesFogAndLeavesExplorationRemembered()
        {
            var rules = Rules();
            var match = new Match(rules, false);
            var first = match.Observe(0);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { Id(match, "worker") }, new SimPoint(16000, 8000)));
            for (int i = 0; i < 80; i++) { match.Step(); AssertMasks(match, rules); }
            var last = match.Observe(0);
            Assert.That(first.Visible.SequenceEqual(last.Visible), Is.False, "Fixture must actually move the vision boundary.");
            for (int i = 0; i < first.Visible.Length; i++) if (first.Visible[i]) Assert.That(last.Explored[i], Is.True);
        }

        [Test]
        public void DeadViewerStopsRevealingCellsImmediatelyButExplorationPersists()
        {
            var rules = Rules();
            rules.Spawns = rules.Spawns.Concat(new[] { Spawn("scout", 0, 14000, 14000), Spawn("raider", 1, 14000, 18000) }).ToArray();
            var match = new Match(rules, false);
            int scout = Id(match, "scout"), raider = Id(match, "raider", 1), cell = 10 * rules.MapWidth + 17;
            Assert.That(match.Observe(0).Visible[cell], Is.True);
            match.QueueCommand(match.MakeCommand(1, CommandKind.Attack, new[] { raider }, targetId: scout));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            Assert.That(match.Find(scout), Is.Null);
            Assert.That(match.Observe(0).Visible[cell], Is.False);
            Assert.That(match.Observe(0).Explored[cell], Is.True);
            AssertMasks(match, rules);
        }

        [Test]
        public void ProducedViewerAndCompletedBuildingPublishTheirSightOnTheCompletingTick()
        {
            var rules = Rules();
            var training = new Match(rules, false);
            int newSightCell = 0;
            Assert.That(training.Observe(0).Visible[newSightCell], Is.False);
            training.QueueCommand(training.MakeCommand(0, CommandKind.Train, new[] { Id(training, "command") }, definitionId: "scout"));
            for (int i = 0; i < 3; i++) { training.Step(); AssertMasks(training, rules); }
            Assert.That(training.Observe(0).Entities.Any(e => e.Owner == 0 && e.DefinitionId == "scout"), Is.True);
            Assert.That(training.Observe(0).Visible[newSightCell], Is.True);

            var building = new Match(rules, false);
            int towerCell = 8 * rules.MapWidth + 19;
            Assert.That(building.Observe(0).Visible[towerCell], Is.False);
            building.QueueCommand(building.MakeCommand(0, CommandKind.Build, new[] { Id(building, "worker") }, new SimPoint(11000, 8000), definitionId: "tower"));
            building.Step();
            Assert.That(building.LastResults.Single().Accepted, Is.True);
            int tower = Id(building, "tower");
            Assert.That(building.Find(tower).Completed, Is.False);
            for (int i = 0; i < 100 && !building.Find(tower).Completed; i++)
            {
                Assert.That(building.Observe(0).Visible[towerCell], Is.False);
                building.Step(); AssertMasks(building, rules);
            }
            Assert.That(building.Find(tower).Completed, Is.True);
            Assert.That(building.Observe(0).Visible[towerCell], Is.True);
        }

        [Test]
        public void RestoreAtSameTickCannotReuseConstructorSpawnMasksAndWallsDoNotOccludeSight()
        {
            var rules = Rules();
            var initial = new Match(rules, false);
            var state = initial.CaptureState();
            state.Entities.Single(e => e.Owner == 0 && e.DefinitionId == "worker").Position = new SimPoint(19500, 12500);
            var restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(state)));
            Assert.That(restored.Tick, Is.Zero);
            AssertMasks(restored, rules);
            Assert.That(restored.Observe(0).Visible[12 * rules.MapWidth + 22], Is.True, "Current visibility is radial even across public blocked terrain.");
            Assert.That(initial.Observe(0).Visible[12 * rules.MapWidth + 22], Is.False);
        }

        [Test, Timeout(20000)]
        public void LiveAiAndJsonContinuationKeepOldRasterForBothPlayersForSixHundredTicks()
        {
            var rules = SimulationFixture.Rules();
            var match = new Match(rules, true, 713);
            Match resumed = null;
            for (int i = 0; i < 600; i++)
            {
                if (i == 100) resumed = Match.Restore(rules, JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(match.CaptureState())));
                match.Step();
                if (resumed != null) resumed.Step();
                AssertMasks(match, rules);
                if (resumed != null && i % 20 == 0)
                {
                    AssertMasks(resumed, rules);
                    Assert.That(resumed.StateHash(), Is.EqualTo(match.StateHash()), "AI/restore changed at " + match.Tick);
                }
            }
        }
    }
}
