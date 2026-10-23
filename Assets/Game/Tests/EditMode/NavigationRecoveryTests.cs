using System;
using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class NavigationRecoveryTests
    {
        private static RuleSet Rules(SimPoint mover, SimPoint? blocker = null)
        {
            var spawns = new List<SpawnDefinition>
            {
                Spawn("base", 0, SimPoint.World(4, 4)),
                Spawn("base", 1, SimPoint.World(124, 92)),
                Spawn("runner", 0, mover)
            };
            if (blocker.HasValue) spawns.Add(Spawn("runner", 0, blocker.Value));
            return new RuleSet
            {
                VersionId = "qa-navigation-recovery-v1", ProductTitle = "Navigation recovery",
                MapName = "Corner and settled unit", TickRate = 20, MapWidth = 128, MapHeight = 96,
                GatherTicks = 1, CargoAmount = 1,
                Definitions = new[]
                {
                    new EntityDefinition { Id = "base", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                        Radius = 1000, Sight = 0, SupplyProvided = 100, BuildTicks = 1 },
                    new EntityDefinition { Id = "runner", MaxHealth = 40, Radius = 350, Speed = 4000,
                        Sight = 0, SupplyCost = 1, BuildTicks = 1 }
                },
                Blocks = new[] { new MapBlock { X = 62, Z = 62, Width = 2, Height = 34 } },
                Spawns = spawns.ToArray()
            };
        }

        private static SpawnDefinition Spawn(string definition, int owner, SimPoint point) =>
            new SpawnDefinition { DefinitionId = definition, Owner = owner, Position = point };

        [Test, Timeout(15000)]
        public void SettledNeighbourDoesNotCauseAvoidanceOscillationAndActiveAvoidanceResumesAfterSave()
        {
            var start = new SimPoint(110764, 42203);
            var blockerPosition = new SimPoint(111500, 42300);
            var destination = new SimPoint(115300, 42300);
            var rules = Rules(start, blockerPosition);
            var match = new Match(rules, false, 2101);
            var runners = match.CaptureState().Entities.Where(e => e.DefinitionId == "runner").OrderBy(e => e.Id).ToArray();
            var moverId = runners[0].Id;
            var blockerId = runners[1].Id;
            Assert.That(DistanceSquared(start, blockerPosition), Is.GreaterThan(700L * 700L), "The initial placement must be nonoverlapping.");
            Assert.That(DistanceSquared(destination, blockerPosition), Is.GreaterThan(700L * 700L), "The destination must be unoccupied.");
            AssertTerrainClear(start, rules);
            AssertTerrainClear(destination, rules);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Hold, new[] { blockerId }));
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { moverId }, destination));
            match.Step();
            Assert.That(match.LastResults.Count, Is.EqualTo(2));
            Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
            for (var tick = 0; tick < 20 && match.Find(moverId).AvoidanceSide == 0; tick++)
            {
                AssertRunnerGeometry(match, rules, moverId, blockerId, blockerPosition);
                match.Step();
            }
            var active = match.Find(moverId);
            Assert.That(active.AvoidanceSide, Is.Not.Zero, "This close blocker scenario must exercise active persisted avoidance.");
            Assert.That(active.Orders.Count, Is.GreaterThan(0), "Save must occur during avoidance, before arrival.");
            var saved = JsonUtility.ToJson(match.CaptureState());
            var resumed = Match.Restore(rules, JsonUtility.FromJson<MatchState>(saved));
            Assert.That(resumed.Find(moverId).AvoidanceSide, Is.EqualTo(active.AvoidanceSide));
            var arrived = false;
            while (match.Tick <= 300 && !arrived)
            {
                AssertRunnerGeometry(match, rules, moverId, blockerId, blockerPosition);
                AssertRunnerGeometry(resumed, rules, moverId, blockerId, blockerPosition);
                Assert.That(resumed.StateHash(), Is.EqualTo(match.StateHash()), "Avoidance continuation diverged at tick " + match.Tick);
                var mover = match.Find(moverId);
                arrived = mover.Orders.Count == 0 && DistanceSquared(mover.Position, destination) <= 250L * 250L;
                if (!arrived && match.Tick < 300) { match.Step(); resumed.Step(); }
                else break;
            }
            Assert.That(arrived, Is.True, "Stable avoidance must get past the settled neighbour within 300 ticks.");
            Assert.That(resumed.StateHash(), Is.EqualTo(match.StateHash()));
        }

        [Test, Timeout(15000)]
        public void LegalPositionNearWallCornerCanReachFreeDestinationBeyondCorner()
        {
            var start = new SimPoint(63895, 61643);
            var destination = new SimPoint(79200, 67000);
            var rules = Rules(start);
            Assert.That(DistanceToWallSquared(start, rules.Blocks.Single()), Is.EqualTo(357L * 357L),
                "The initial 357mm clearance is valid for radius 350, despite the nearby cell-centre corner.");
            AssertTerrainClear(start, rules);
            AssertTerrainClear(destination, rules);
            // A wide-clearance geometric witness proves this request is reachable:
            // first move south, then east of the wall, then north to the goal row.
            var witness = new[] { start, new SimPoint(63895, 61000), new SimPoint(65000, 61000),
                new SimPoint(65000, 67000), destination };
            for (var index = 1; index < witness.Length; index++) AssertClearWitnessSegment(witness[index - 1], witness[index], rules);
            var match = new Match(rules, false, 2102);
            var moverId = match.CaptureState().Entities.Single(e => e.DefinitionId == "runner").Id;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, moverId, destination).Accepted, Is.True);
            var arrived = false;
            for (var tick = match.Tick; tick <= 500 && !arrived; tick++)
            {
                var mover = match.Find(moverId);
                AssertTerrainClear(mover.Position, rules);
                arrived = mover.Orders.Count == 0 && DistanceSquared(mover.Position, destination) <= 250L * 250L;
                if (!arrived && tick < 500) match.Step();
            }
            Assert.That(arrived, Is.True,
                "A legal start with a proven free route cannot remain stuck because its cell-centre anchor cannot connect.");
        }

        [Test, Timeout(15000)]
        public void LegalPositionAtRoundedLowerLeftCornerCanConnectToGridAndReachDestination()
        {
            var start = new SimPoint(61873, 61651);
            var destination = new SimPoint(79200, 67000);
            var rules = Rules(start);
            Assert.That(DistanceToWallSquared(start, rules.Blocks.Single()), Is.EqualTo(127L * 127L + 349L * 349L));
            Assert.That(DistanceToWallSquared(start, rules.Blocks.Single()), Is.GreaterThan(350L * 350L),
                "The lower-left rounded-corner start is legal, even though its axis clearances alone are below the radius.");
            AssertTerrainClear(start, rules);
            AssertTerrainClear(destination, rules);
            var witness = new[] { start, new SimPoint(61873, 61000), new SimPoint(65000, 61000),
                new SimPoint(65000, 67000), destination };
            for (var index = 1; index < witness.Length; index++) AssertClearWitnessSegment(witness[index - 1], witness[index], rules);
            var match = new Match(rules, false, 2105);
            var moverId = match.CaptureState().Entities.Single(e => e.DefinitionId == "runner").Id;
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, moverId, destination).Accepted, Is.True);
            var arrived = false;
            while (match.Tick <= 500 && !arrived)
            {
                var mover = match.Find(moverId);
                AssertTerrainClear(mover.Position, rules);
                arrived = mover.Orders.Count == 0 && DistanceSquared(mover.Position, destination) <= 250L * 250L;
                if (!arrived && match.Tick < 500) match.Step();
                else break;
            }
            Assert.That(arrived, Is.True,
                "A legal rounded-corner start must connect to the reachable grid route without intersecting terrain.");
        }

        [Test, Timeout(15000)]
        public void ExactEndpointNearBuildingUsesSafeConnectorThenExecutesAppendedMove()
        {
            var start = new SimPoint(8500, 10000);
            var center = new SimPoint(10500, 10500);
            var exactGoal = new SimPoint(11486, 10674);
            var finalGoal = new SimPoint(15000, 14000);
            var nearestCell = new SimPoint(11500, 10500);
            const int combinedRadius = 1000;
            var rules = Rules(start);
            rules.Blocks = new MapBlock[0];
            rules.Definitions.Single(definition => definition.Id == "runner").Speed = 3000;
            rules.Definitions = rules.Definitions.Concat(new[]
            {
                new EntityDefinition { Id = "obstacle", IsBuilding = true, Radius = 650,
                    MaxHealth = 1000, Sight = 0, BuildTicks = 1 }
            }).ToArray();
            rules.Spawns = rules.Spawns.Concat(new[] { Spawn("obstacle", 0, center) }).ToArray();
            Assert.That(DistanceSquared(exactGoal, center), Is.EqualTo(986L * 986L + 174L * 174L));
            Assert.That(DistanceSquared(exactGoal, center), Is.GreaterThan((long)combinedRadius * combinedRadius),
                "The exact requested endpoint must be legally outside the combined building/unit footprint.");
            Assert.That(SegmentCircleClearanceSquared(nearestCell, exactGoal, center), Is.LessThan((double)combinedRadius * combinedRadius),
                "The nearest cell-centre connector crosses the footprint, so simply snapping across it is invalid.");
            var match = new Match(rules, false, 2106);
            var moverId = match.CaptureState().Entities.Single(entity => entity.DefinitionId == "runner").Id;
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { moverId }, exactGoal));
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, new[] { moverId }, finalGoal, append: true));
            var previous = start;
            var visitedExactGoal = false;
            var arrived = false;
            for (var tick = 0; tick < 500 && !arrived; tick++)
            {
                match.Step();
                if (tick == 0)
                {
                    Assert.That(match.LastResults.Count, Is.EqualTo(2));
                    Assert.That(match.LastResults.All(result => result.Accepted), Is.True);
                }
                var mover = match.Find(moverId);
                AssertTerrainClear(mover.Position, rules);
                Assert.That(DistanceSquared(mover.Position, center), Is.GreaterThanOrEqualTo((long)combinedRadius * combinedRadius));
                Assert.That(SegmentCircleClearanceSquared(previous, mover.Position, center),
                    Is.GreaterThanOrEqualTo((double)combinedRadius * combinedRadius - .000001),
                    "A short connector cannot cross the circle between otherwise legal tick positions.");
                visitedExactGoal |= DistanceSquared(mover.Position, exactGoal) == 0;
                previous = mover.Position;
                arrived = mover.Orders.Count == 0 && DistanceSquared(mover.Position, finalGoal) <= 250L * 250L;
            }
            Assert.That(visitedExactGoal, Is.True, "The first exact Move must reach its legal endpoint before advancing the queue.");
            Assert.That(arrived, Is.True, "The appended Move must finish within 500 ticks after endpoint recovery.");
        }

        [Test]
        public void OwnObservationRedactsPersistedAvoidanceSide()
        {
            var rules = Rules(new SimPoint(110764, 42203));
            var match = new Match(rules, false, 2103);
            var snapshot = match.CaptureState();
            var mover = snapshot.Entities.Single(e => e.DefinitionId == "runner");
            mover.AvoidanceSide = 1;
            match = Match.Restore(rules, snapshot);
            Assert.That(match.Find(mover.Id).AvoidanceSide, Is.EqualTo(1), "The diagnostic snapshot must really contain the persisted field.");
            Assert.That(match.Observe(0).Entities.Single(e => e.Id == mover.Id).AvoidanceSide, Is.Zero,
                "Authoritative navigation bookkeeping is not part of the player's observation.");
        }

        [Test, Timeout(15000)]
        public void OccupiedCoarseWaypointsDoNotHideFreeIntermediateGaps()
        {
            var start = new SimPoint(90500, 66000);
            var destination = new SimPoint(93000, 73000);
            var blockerPositions = new[]
            {
                new SimPoint(90600, 67000),
                new SimPoint(90600, 68900),
                new SimPoint(90600, 70800)
            };
            var coarse = new[]
            {
                new SimPoint(90500, 67500), new SimPoint(90500, 68500),
                new SimPoint(90500, 69500), new SimPoint(90500, 70500), destination
            };
            var gap = new SimPoint(90500, 67950);
            var rules = Rules(start);
            rules.Spawns = rules.Spawns.Concat(blockerPositions.Select(point => Spawn("runner", 0, point))).ToArray();
            var match = new Match(rules, false, 2104);
            var snapshot = match.CaptureState();
            var runners = snapshot.Entities.Where(e => e.DefinitionId == "runner").OrderBy(e => e.Id).ToArray();
            var moverId = runners[0].Id;
            var settled = runners.Skip(1).ToDictionary(e => e.Id, e => e.Position);
            // Reproduce an already accepted, coarse navigation path through a crowd.
            // This saved public DTO is legal: dynamic actors can settle onto its nodes after planning.
            runners[0].Orders.Add(new UnitOrder { Kind = OrderKind.Move, Target = destination });
            runners[0].Path = coarse.ToList();
            runners[0].PathIndex = 0;
            runners[0].PathTarget = destination;
            runners[0].PathStoppingDistance = 0;
            foreach (var blocker in runners.Skip(1)) blocker.Orders.Add(new UnitOrder { Kind = OrderKind.Hold });
            foreach (var point in coarse.Take(4))
                Assert.That(blockerPositions.Any(blocker => DistanceSquared(point, blocker) < 700L * 700L), Is.True,
                    "All four whole-waypoint candidates must really be occupied by settled neighbours.");
            Assert.That(gap.X, Is.EqualTo(coarse[0].X));
            Assert.That(gap.Z, Is.GreaterThan(coarse[0].Z).And.LessThan(coarse[1].Z));
            foreach (var blocker in blockerPositions)
            {
                Assert.That(DistanceSquared(gap, blocker), Is.GreaterThan(700L * 700L), "An intermediate subpoint between occupied whole nodes is free.");
                Assert.That(DistanceSquared(destination, blocker), Is.GreaterThan(700L * 700L));
                Assert.That(DistanceSquared(start, blocker), Is.GreaterThan(700L * 700L));
            }
            for (var index = 1; index < blockerPositions.Length; index++)
                Assert.That(DistanceSquared(blockerPositions[index - 1], blockerPositions[index]), Is.EqualTo(1900L * 1900L));
            foreach (var point in coarse.Concat(blockerPositions).Concat(new[] { start, gap })) AssertTerrainClear(point, rules);
            match = Match.Restore(rules, snapshot);
            var arrived = false;
            while (match.Tick <= 1000 && !arrived)
            {
                var mover = match.Find(moverId);
                AssertTerrainClear(mover.Position, rules);
                foreach (var pair in settled)
                {
                    var blocker = match.Find(pair.Key);
                    Assert.That(DistanceSquared(blocker.Position, pair.Value), Is.Zero, "The coarse-node blockers must remain settled.");
                    Assert.That(DistanceSquared(mover.Position, blocker.Position), Is.GreaterThanOrEqualTo(700L * 700L),
                        "Intermediate-point recovery cannot overlap a stationary neighbour.");
                }
                arrived = mover.Orders.Count == 0 && DistanceSquared(mover.Position, destination) <= 250L * 250L;
                if (!arrived && match.Tick < 1000) match.Step();
                else break;
            }
            Assert.That(arrived, Is.True,
                "Four occupied coarse nodes cannot conceal the physically free gaps between those nodes forever.");
        }

        private static void AssertRunnerGeometry(Match match, RuleSet rules, int moverId, int blockerId, SimPoint blockerPosition)
        {
            var mover = match.Find(moverId);
            var blocker = match.Find(blockerId);
            Assert.That(DistanceSquared(mover.Position, blocker.Position), Is.GreaterThanOrEqualTo(700L * 700L),
                "Local avoidance cannot overlap the settled friendly.");
            Assert.That(DistanceSquared(blocker.Position, blockerPosition), Is.Zero, "The Hold blocker must stay fixed.");
            AssertTerrainClear(mover.Position, rules);
            AssertTerrainClear(blocker.Position, rules);
        }

        private static void AssertClearWitnessSegment(SimPoint from, SimPoint to, RuleSet rules)
        {
            var distance = Math.Sqrt(DistanceSquared(from, to));
            var samples = Math.Max(1, (int)Math.Ceiling(distance / 100));
            for (var index = 0; index <= samples; index++)
                AssertTerrainClear(new SimPoint(from.X + (to.X - from.X) * index / samples,
                    from.Z + (to.Z - from.Z) * index / samples), rules);
        }

        private static void AssertTerrainClear(SimPoint point, RuleSet rules)
        {
            Assert.That(point.X, Is.GreaterThanOrEqualTo(350));
            Assert.That(point.Z, Is.GreaterThanOrEqualTo(350));
            Assert.That(point.X, Is.LessThan(rules.MapWidth * 1000 - 350));
            Assert.That(point.Z, Is.LessThan(rules.MapHeight * 1000 - 350));
            foreach (var block in rules.Blocks)
                Assert.That(DistanceToWallSquared(point, block), Is.GreaterThanOrEqualTo(350L * 350L),
                    "A runner's footprint intersects the wall at " + point.X + "," + point.Z);
        }

        private static long DistanceToWallSquared(SimPoint point, MapBlock block)
        {
            var closest = new SimPoint(Math.Max(block.X * 1000, Math.Min(point.X, (block.X + block.Width) * 1000)),
                Math.Max(block.Z * 1000, Math.Min(point.Z, (block.Z + block.Height) * 1000)));
            return DistanceSquared(point, closest);
        }

        private static long DistanceSquared(SimPoint left, SimPoint right)
        {
            long dx = left.X - right.X, dz = left.Z - right.Z;
            return dx * dx + dz * dz;
        }

        private static double SegmentCircleClearanceSquared(SimPoint from, SimPoint to, SimPoint center)
        {
            double dx = to.X - from.X, dz = to.Z - from.Z;
            var lengthSquared = dx * dx + dz * dz;
            var projection = lengthSquared == 0 ? 0 :
                Math.Max(0, Math.Min(1, ((center.X - from.X) * dx + (center.Z - from.Z) * dz) / lengthSquared));
            var offsetX = from.X + projection * dx - center.X;
            var offsetZ = from.Z + projection * dz - center.Z;
            return offsetX * offsetX + offsetZ * offsetZ;
        }
    }
}
