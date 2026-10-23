using System;
using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using NUnit.Framework;

namespace ColonyRTS.Tests
{
    [TestFixture]
    public sealed class GroupMovementTests
    {
        private static RuleSet Rules()
        {
            var spawns = new List<SpawnDefinition>
            {
                SimulationFixture.Spawn("base", 0, 4, 4),
                SimulationFixture.Spawn("base", 1, 36, 28)
            };
            for (var row = 0; row < 4; row++)
                for (var col = 0; col < 4; col++)
                    spawns.Add(SimulationFixture.Spawn("runner", 0, 7.5f + col * 1.2f, 12 + row * 1.2f));
            return new RuleSet
            {
                VersionId = "qa-group-movement-v1", ProductTitle = "Group contract", MapName = "Broad gate",
                TickRate = 20, MapWidth = 40, MapHeight = 32,
                GatherTicks = 3, CargoAmount = 5,
                Definitions = new[]
                {
                    new EntityDefinition { Id = "base", IsBase = true, IsBuilding = true, MaxHealth = 1000,
                        Radius = 1000, Sight = 3000, SupplyProvided = 1000, BuildTicks = 1 },
                    new EntityDefinition { Id = "runner", MaxHealth = 40, Radius = 350, Speed = 4000,
                        Sight = 3500, SupplyCost = 1, BuildTicks = 1 }
                },
                Blocks = new[]
                {
                    new MapBlock { X = 20, Z = 0, Width = 2, Height = 12 },
                    new MapBlock { X = 20, Z = 20, Width = 2, Height = 12 }
                },
                Spawns = spawns.ToArray()
            };
        }

        [Test, Timeout(20000)]
        public void SixteenMoversCrossBroadChokeAndArriveWithoutOverlapOrWallCrossing()
        {
            var rules = Rules();
            var match = new Match(rules, false, 187);
            var ids = match.CaptureState().Entities.Where(e => e.DefinitionId == "runner").Select(e => e.Id).ToArray();
            var target = SimPoint.World(30, 16);
            match.QueueCommand(match.MakeCommand(0, CommandKind.Move, ids, target));
            match.Step();
            Assert.That(match.LastResults.Single().Accepted, Is.True);
            var assigned = match.CaptureState().Entities.Where(e => e.DefinitionId == "runner")
                .ToDictionary(e => e.Id, e => e.Orders.Single().Target);
            foreach (var point in assigned.Values)
            {
                long dx = point.X - target.X, dz = point.Z - target.Z;
                Assert.That(dx * dx + dz * dz, Is.LessThanOrEqualTo(6000L * 6000L), "Group slots must stay within 6m of the requested centre.");
            }
            var arrived = false;
            for (var tick = 0; tick < 1000 && !arrived; tick++)
            {
                var movers = match.CaptureState().Entities.Where(e => e.DefinitionId == "runner").ToArray();
                ValidateGeometry(movers, rules);
                arrived = movers.All(e => e.Orders.Count == 0 && DistanceSquared(e.Position, assigned[e.Id]) <= 250L * 250L);
                if (!arrived) match.Step();
            }
            Assert.That(arrived, Is.True, "All 16 movers must reach their individually assigned slots within 1000 ticks (50 simulated seconds).");
        }

        [Test, Timeout(10000)]
        public void GroupSlotAssignmentIsStableWhenSelectionOrderIsReversed()
        {
            var left = new Match(Rules(), false, 188);
            var right = new Match(Rules(), false, 188);
            var ids = left.CaptureState().Entities.Where(e => e.DefinitionId == "runner").Select(e => e.Id).ToArray();
            left.QueueCommand(left.MakeCommand(0, CommandKind.Move, ids, SimPoint.World(30, 16)));
            right.QueueCommand(right.MakeCommand(0, CommandKind.Move, ids.Reverse().ToArray(), SimPoint.World(30, 16)));
            for (var tick = 0; tick < 40; tick++)
            {
                left.Step();
                right.Step();
                Assert.That(left.StateHash(), Is.EqualTo(right.StateHash()), "Input enumeration order changed group behaviour at tick " + tick);
            }
        }

        [Test, Timeout(15000)]
        public void SettledFriendlyOccupyingIntermediateWaypointDoesNotTrapMover()
        {
            var rules = Rules();
            var spawns = rules.Spawns.Where(spawn => spawn.DefinitionId != "runner").ToList();
            spawns.Add(SimulationFixture.Spawn("runner", 0, 8, 8));
            spawns.Add(SimulationFixture.Spawn("runner", 0, 8, 24));
            rules.Spawns = spawns.ToArray();
            var match = new Match(rules, false, 189);
            var runners = match.CaptureState().Entities.Where(e => e.DefinitionId == "runner").OrderBy(e => e.Id).ToArray();
            var moverId = runners[0].Id;
            var blockerId = runners[1].Id;
            var destination = SimPoint.World(30, 8);
            Assert.That(SimulationFixture.Send(match, 0, CommandKind.Move, moverId, destination).Accepted, Is.True);
            var snapshot = match.CaptureState();
            var mover = snapshot.Entities.Single(e => e.Id == moverId);
            var intermediate = mover.Path.Skip(mover.PathIndex).Where(point =>
                DistanceSquared(point, destination) > 5000L * 5000L &&
                DistanceSquared(point, mover.Position) > 1500L * 1500L).ToArray();
            Assert.That(intermediate, Is.Not.Empty,
                "The wall detour must produce a real intermediate waypoint more than 5m from the free destination.");
            var occupiedWaypoint = intermediate[0];
            var blocker = snapshot.Entities.Single(e => e.Id == blockerId);
            blocker.Position = occupiedWaypoint;
            blocker.Orders.Add(new UnitOrder { Kind = OrderKind.Hold });
            Assert.That(mover.Path.Skip(mover.PathIndex).Any(point => DistanceSquared(point, blocker.Position) == 0), Is.True,
                "The settled friendly must occupy an actual remaining waypoint, not an arbitrary nearby point.");
            Assert.That(DistanceSquared(blocker.Position, destination), Is.GreaterThan(5000L * 5000L));
            match = Match.Restore(rules, snapshot);
            var arrived = false;
            for (var tick = 0; tick < 1000 && !arrived; tick++)
            {
                runners = match.CaptureState().Entities.Where(e => e.DefinitionId == "runner").ToArray();
                ValidateGeometry(runners, rules);
                mover = runners.Single(e => e.Id == moverId);
                blocker = runners.Single(e => e.Id == blockerId);
                Assert.That(DistanceSquared(blocker.Position, occupiedWaypoint), Is.Zero,
                    "The Hold blocker must remain settled while the mover navigates around it.");
                arrived = mover.Orders.Count == 0 && DistanceSquared(mover.Position, destination) <= 250L * 250L;
                if (!arrived) match.Step();
            }
            Assert.That(arrived, Is.True,
                "A friendly on a distant intermediate waypoint cannot trap an otherwise reachable move forever.");
        }

        private static long DistanceSquared(SimPoint left, SimPoint right)
        {
            long dx = left.X - right.X, dz = left.Z - right.Z;
            return dx * dx + dz * dz;
        }

        private static void ValidateGeometry(EntityState[] movers, RuleSet rules)
        {
            for (var i = 0; i < movers.Length; i++)
            {
                var p = movers[i].Position;
                foreach (var block in rules.Blocks)
                {
                    var closest = new SimPoint(Math.Max(block.X * 1000, Math.Min(p.X, (block.X + block.Width) * 1000)),
                        Math.Max(block.Z * 1000, Math.Min(p.Z, (block.Z + block.Height) * 1000)));
                    Assert.That(DistanceSquared(p, closest), Is.GreaterThanOrEqualTo(350L * 350L), "A mover's footprint crossed wall terrain.");
                }
                for (var j = i + 1; j < movers.Length; j++)
                    Assert.That(DistanceSquared(p, movers[j].Position), Is.GreaterThanOrEqualTo(700L * 700L),
                        "Movers " + movers[i].Id + " and " + movers[j].Id + " overlapped.");
            }
        }
    }
}
